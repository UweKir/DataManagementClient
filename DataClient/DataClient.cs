using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

using ICSharpCode.SharpZipLib.Zip;
using DataManager.Logging;
using DataManager.Network;
using DataManager.HouseKeeping;


namespace DataManagement.DataClientLib
{

    public class DataClient
    {
        private static LogService logger;

        public static String Location { get; set; }
        /// <summary>
        /// Folder to search for files
        /// </summary>
        private String scanFolder;

        /// <summary>
        /// Folder to copy files
        /// </summary>
        private String workingFolder;

        /// <summary>
        /// Folder for files to send
        /// </summary>
        private String queueFolder;

        /// <summary>
        /// Files could not be worked with
        /// </summary>
        private String suspectFolder;

        private object workerSynch;

        private int workerInterval;

        private System.Timers.Timer tiWorker;

        private int aliveInterval;

        private AliveAgent aliveAgent;

        private DataManager.HouseKeeping.FileZipAgent houseKeepingAgent;

        private bool workerIsBusy;

        private bool initOK;

        private String configFolder;

        public DataClient()
        {
            configFolder = @"C:\KTBDataManager\Programs\Config\KTBDataManagerClient.ini";
            init();
        }

        public DataClient(String pathConfigFolder)
        {
            configFolder = pathConfigFolder;
            init();
        }

        private void init()
        {
            try
            {
                logger = LogService.Instance();

                workerSynch = new object();

                workerIsBusy = false;

                tiWorker = new System.Timers.Timer();
                tiWorker.Interval = 10000;
                tiWorker.Elapsed += TiWorker_Elapsed;

               
                if(!File.Exists(configFolder))
                {
                    initOK = false;
                    logEntry(System.Diagnostics.EventLogEntryType.Error, "DataClient.init failed: " + configFolder + " does not exist");

                    return;
                }

                IniFile ini = new IniFile(configFolder);
                LogService.LogPath = ini.Read("LogPath", "PATH");

                Location = ini.Read("Location", "GENERAL");
                workerInterval = Int32.Parse(ini.Read("ScanIntervalMinutes", "GENERAL")) * 1000 * 60;
                aliveInterval = Int32.Parse(ini.Read("AliveIntervalMinutes", "GENERAL")) * 1000 * 60;

                scanFolder = ini.Read("ScanFolder", "PATH");
                workingFolder = ini.Read("WorkingFolder", "PATH");
                queueFolder = ini.Read("QueueFolder", "PATH");
                suspectFolder = ini.Read("SuspectFolder", "PATH");

               
                DataManager.Network.TCPClient.ServerAddress = ini.Read("ServiceAddress", "CONNECTION");
                DataManager.Network.TCPClient.ServerPort = Int32.Parse(ini.Read("ServicePort", "CONNECTION"));

                DataFile.FileNameRegEx = ini.Read("FileNameRegEx", "FORMAT");
                DataFile.FileContentHeaderRegEx = ini.Read("ContentHeaderRegEx", "FORMAT");
                DataFile.DateTimeFormat = ini.Read("ContentDateTimeFormat", "FORMAT");

                aliveAgent = new AliveAgent(aliveInterval, Location);

                String daysToZipLog = ini.Read("DaysToZip", "HOUSEKEEPING");
                String daysToDeleteLog = ini.Read("DaysToDelete", "HOUSEKEEPING");

                houseKeepingAgent = new FileZipAgent(LogService.LogPath, daysToZipLog, daysToDeleteLog);

                if (!houseKeepingAgent.init())
                {
                    logEntry(System.Diagnostics.EventLogEntryType.Error, "DataClient.init failed: Check housekeeping parameter");
                    initOK = false;

                    return;
                }

                initOK = true;
                logEntry(System.Diagnostics.EventLogEntryType.Information, "DataClient.init ok");

            }
            catch(Exception ex)
            {
                initOK = false;
                logEntry(System.Diagnostics.EventLogEntryType.Error, "DataClient.init failed: " + ex.Message);
            }

            
        }

        public void startWorker()
        {
            if (initOK)
            {
                tiWorker.Start();

                if(aliveInterval > 1000)
                {
                    aliveAgent.WorkerTimerTick = aliveInterval;
                    aliveAgent.start();
                    houseKeepingAgent.Start();
                }
                
            }
        }

        public void stopWorker()
        {
            tiWorker.Stop();
            aliveAgent.stop();
            houseKeepingAgent.Terminate();
        }

        private void TiWorker_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (workerIsBusy)
                return;

            lock (workerSynch)
            {
                try
                {
                    workerIsBusy = true;

                    tiWorker.Interval = workerInterval;

                    createFolders();
                    scanForFiles();
                 
                }
                catch(Exception ex)
                {
                    logEntry(System.Diagnostics.EventLogEntryType.Error, "DataClient.TiWorker_Elapsed: failed: " + ex.Message);
                }
                finally
                {
                    workerIsBusy = false;
                }
               
            }
        }

        public void scanForFiles()
        {
            
                DirectoryInfo diScan = new DirectoryInfo(scanFolder);
                DirectoryInfo diQueue = new DirectoryInfo(queueFolder);

                List<FileInfo> lstFiles = new List<FileInfo>();

                try
                {

              
                if (!diQueue.Exists)
                    Directory.CreateDirectory(queueFolder);

                if (!diScan.Exists)
                    Directory.CreateDirectory(scanFolder);

                // First move eventually old files in working folder to the queue-folder
                List<FileInfo> lstOldFiles = new DirectoryInfo(workingFolder).GetFiles().ToList();

                foreach (FileInfo oldFi in lstOldFiles)
                {
                    moveToQueueFolder(oldFi);
                }

                // first handle the files in the queue folder
                lstFiles = diQueue.GetFiles().ToList();
                handleDataFileList(lstFiles);

                // now handle files in the scan folder
                lstFiles = diScan.GetFiles().ToList();
                handleDataFileList(lstFiles);


                }
                catch (Exception ex)
                {
                    logEntry(System.Diagnostics.EventLogEntryType.Error, "DataClient.scanForFiles failed: " + ex.Message);
                }
            
        }

        private void handleDataFileList(List<FileInfo> lstFiles)
        {
            if (lstFiles == null)
                return;

            foreach(FileInfo fi in lstFiles)
            {

                if (!Directory.Exists(workingFolder))
                    Directory.CreateDirectory(workingFolder);

                // check if file is in use by another process
                if (IsFileLocked(fi))
                    continue;

                // Copy the file to the working folder
                String workingFile = Path.Combine(workingFolder, fi.Name);
                File.Copy(fi.FullName,workingFile, true);
                File.Delete(fi.FullName);

                FileInfo fiWorking = new FileInfo(workingFile);

                DataError err = handleFile(ref fiWorking);

                switch (err)
                {
                  
                    case DataError.NO_ERROR:
                        moveToArchiveFolder(fiWorking);
                        break;
                    case DataError.DB_ERROR:
                        moveToQueueFolder(fiWorking);
                        break;
                    case DataError.UNVALID_FORMAT:
                        moveToSuspectFolder(fiWorking);
                        break;
                    case DataError.CONNECTION_FAILED:
                        moveToQueueFolder(fiWorking);
                        break;
                    default:
                        moveToSuspectFolder(fiWorking);
                        break;

                }
            }

        }

        private DataError handleFile(ref FileInfo fi)
        {
            // unzip file if required
            if(fi.Extension.ToUpper() == ".ZIP")
            {

                fi = getFileFromZip(fi);

                if (fi == null)
                {
                    return DataError.UNVALID_FORMAT;
                }
            }

            // Check the filename
            if(Regex.IsMatch(fi.Name, DataFile.FileNameRegEx))
            {
                MatchCollection matches = Regex.Matches(fi.Name.Trim(), DataFile.FileNameRegEx);

                // read data from matches in filename string
                string type = "";
                string location = "";

                foreach (Match m in matches)
                {
                    type = m.Groups["TYPE"].Value;
                    location = m.Groups["LOCATION"].Value;
                }

                DataFile dataFile = null;

                // create the datafile according to type
                switch(type)
                {
                    case "Produktion":
                        dataFile = new ProcuctionFile(fi, location);
                        break;
                    case "Energie":
                        dataFile = new ConsumptionFile(fi, location);
                        break;
                    case "Wasser":
                        dataFile = new ConsumptionFile(fi, location);
                        break;
                    default:
                        dataFile = null;
                        break;
                        
                }

                if (dataFile != null)
                    return dataFile.sendFile();


            }

            return DataError.UNVALID_FORMAT;
   
        }

        #region Util

        private void createFolders()
        {
           
            if (!Directory.Exists(scanFolder))
                Directory.CreateDirectory(scanFolder);

            if (!Directory.Exists(workingFolder))
                Directory.CreateDirectory(workingFolder);

            if (!Directory.Exists(queueFolder))
                Directory.CreateDirectory(queueFolder);

            if (!Directory.Exists(suspectFolder))
                Directory.CreateDirectory(suspectFolder);

        }

        private FileInfo getFileFromZip(FileInfo fi)
        {
            DirectoryInfo diWorkingFolder = new DirectoryInfo(workingFolder);

            if (!ExtractZipFile(fi.FullName, "", workingFolder))
                return null;

            // now the extracted file
            List<FileInfo> lstExtractedFiles = diWorkingFolder.GetFiles().ToList();

            // should be only 2 files in working folder now
            if (lstExtractedFiles.Count > 2)
            {
                // move all files to suspect
                foreach (FileInfo fiExtracted in lstExtractedFiles)
                {
                    moveToSuspectFolder(fiExtracted);
                }

                return null;
            }

            File.Delete(fi.FullName);

            lstExtractedFiles = diWorkingFolder.GetFiles().ToList();

            if (lstExtractedFiles.Count > 0)
                return new FileInfo(lstExtractedFiles[0].FullName);

            return null;

        }

        private void moveToArchiveFolder(FileInfo fi)
        {
            logger.ArchiveFile("DataClient", fi.FullName);
            logEntry(System.Diagnostics.EventLogEntryType.Information, "DataClient.moveToAchiveFolder " + fi.Name);
        }

        private void moveToSuspectFolder(FileInfo fi)
        {
            if (!Directory.Exists(suspectFolder))
                Directory.CreateDirectory(suspectFolder);

            File.Copy(fi.FullName, Path.Combine(suspectFolder, fi.Name), true);
            File.Delete(fi.FullName);

            logEntry(System.Diagnostics.EventLogEntryType.Error, "DataClient.moveToSuspectFolder " + fi.Name);
        }

        private void moveToQueueFolder(FileInfo fi)
        {
            if (!Directory.Exists(queueFolder))
                Directory.CreateDirectory(queueFolder);

            File.Copy(fi.FullName, Path.Combine(queueFolder, fi.Name), true);
            File.Delete(fi.FullName);

            logEntry(System.Diagnostics.EventLogEntryType.Error, "DataClient.moveToQueueFolder " + fi.Name);
        }

        public static void logEntry(System.Diagnostics.EventLogEntryType type, String message)
        {
            logger.WriteEntry(type, message, "DataClient");
        }

        public bool ExtractZipFile(string archiveFilenameIn, string password, string outFolder)
        {
            ZipFile zf = null;
            bool success = false;

            try
            {
                FileStream fs = File.OpenRead(archiveFilenameIn);
                zf = new ZipFile(fs);
                if (!String.IsNullOrEmpty(password))
                {
                    zf.Password = password;     // AES encrypted entries are handled automatically
                }
                foreach (ZipEntry zipEntry in zf)
                {
                    if (!zipEntry.IsFile)
                    {
                        continue;           // Ignore directories
                    }
                    String entryFileName = zipEntry.Name;
                    // to remove the folder from the entry:- entryFileName = Path.GetFileName(entryFileName);
                    // Optionally match entrynames against a selection list here to skip as desired.
                    // The unpacked length is available in the zipEntry.Size property.

                    byte[] buffer = new byte[4096];     // 4K is optimum
                    Stream zipStream = zf.GetInputStream(zipEntry);

                    // Manipulate the output filename here as desired.
                    String fullZipToPath = Path.Combine(outFolder, entryFileName);
                    string directoryName = Path.GetDirectoryName(fullZipToPath);
                    if (directoryName.Length > 0)
                        Directory.CreateDirectory(directoryName);

                    // Unzip file in buffered chunks. This is just as fast as unpacking to a buffer the full size
                    // of the file, but does not waste memory.
                    // The "using" will close the stream even if an exception occurs.
                    using (FileStream streamWriter = File.Create(fullZipToPath))
                    {
                        ICSharpCode.SharpZipLib.Core.StreamUtils.Copy(zipStream, streamWriter, buffer);
                    }

                    success = true;

                }
            }
            catch(Exception ex)
            {
                success = false;
                logEntry(System.Diagnostics.EventLogEntryType.Error, "DataClient.ExtractZipFile: " + ex.Message);
            }
            finally
            {
                if (zf != null)
                {
                    zf.IsStreamOwner = true; // Makes close also shut the underlying stream
                    zf.Close(); // Ensure we release resources
                }
            }

            return success;
        }

        private bool IsFileLocked(FileInfo file)
        {
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return false;
        }

        #endregion




    }
}
