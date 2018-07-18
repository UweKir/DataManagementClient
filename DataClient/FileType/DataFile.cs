using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;

namespace DataManagement.DataClientLib
{
    public enum DataError
    {
        NO_ERROR,
        CONNECTION_FAILED,
        DB_ERROR,
        UNVALID_FORMAT,
        OTHERS
    }

    public abstract class DataFile
    {
        /// <summary>
        /// Regular expression of source file names
        /// </summary>
        public static String FileNameRegEx;

        /// <summary>
        /// Regular expression of the header in the data list of this source files
        /// </summary>
        public static String FileContentHeaderRegEx;

        /// <summary>
        /// datetime format in the data lines of csv
        /// </summary>
        public static String DateTimeFormat;

        protected FileInfo fiSource;

        protected String locationName;

        protected int lineCounter;

        /// <summary>
        /// Headers in the csv-header line
        /// </summary>
        protected List<Header> lstHeader;

        public DataFile(FileInfo fiSource, String locationName)
        {
            this.fiSource = fiSource;
            this.locationName = locationName;
           
            lineCounter = 0;
        }

        protected abstract void sendDataLine(List<String> aLine);


        public DataError sendFile()
        {
            try
            {
                bool firstLine = true;

                using (StreamReader read = new StreamReader(fiSource.FullName))
                {
                   
                   String line;

                   while ((line = read.ReadLine()) != null)
                   {
                        // read the single lines and remove the ' " '
                        List<String> aLine = line.Replace("\"", "").Split(';').ToList();

                        if(firstLine)
                        {
                            firstLine = false;
                            readHeadLine(aLine);

                        }
                        else
                        {
                            lineCounter++;
                            sendDataLine(aLine);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                DataClient.logEntry(System.Diagnostics.EventLogEntryType.Error, "DataFile.sendFile fails : " + ex.Message);
                Console.WriteLine("DataFile.sendFile fails : " + ex.Message);

                if (ex is ArgumentNullException 
                  || ex is IOException 
                  || ex is EncoderFallbackException 
                  || ex is System.Net.Sockets.SocketException 
                  || ex is ArgumentOutOfRangeException
                  || ex is ObjectDisposedException)
                {
                    return DataError.CONNECTION_FAILED;
                }

                if (ex is DataException.RemoteDBException)
                    return DataError.DB_ERROR;

                if (ex is DataException.DataFormatException)
                    return DataError.UNVALID_FORMAT;

                return DataError.UNVALID_FORMAT;
            }


            return DataError.NO_ERROR;
        }


        protected void readHeadLine(List<String> lst)
        {
            lstHeader = new List<Header>();

            // begin to read the second entry, first entry is the timestamp
            if (lst.Count > 2)
            {
                for (int i = 1; i < lst.Count; i++)
                {
                    String strHeader = lst[i];
                   
                    Header h = new Header();

                    if (Regex.IsMatch(strHeader, DataFile.FileContentHeaderRegEx))
                    {
                        MatchCollection matches = Regex.Matches(strHeader, DataFile.FileContentHeaderRegEx);

                        // read header data from matches in csv-header string
                        foreach (Match m in matches)
                        {
                            h.Key = strHeader;
                            h.Article = m.Groups["ARTICLE"].Value;
                            h.Unity = m.Groups["UNITY"].Value;
                            h.Device = m.Groups["DEVICE"].Value;
                        }
                    }

                    lstHeader.Add(h);


                }
            }
        }

        protected void handleDataReplyError(int errorCode)
        {
            if (errorCode > 100 && errorCode < 200)
                throw new IOException();
            else
            {

                if (errorCode > 300 && errorCode < 400)
                    throw new DataException.RemoteDBException();
                else
                    throw new DataException.DataFormatException();
            }
        }

    }
}
