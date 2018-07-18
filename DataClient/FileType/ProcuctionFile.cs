using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Sockets;
using DataManager.Network;
using DataManager.Network.JSON;

namespace DataManagement.DataClientLib
{
    /// <summary>
    /// Class to handle the production files
    /// </summary>
    public class ProcuctionFile: DataFile
    {

        public ProcuctionFile(FileInfo fi, String locationName)
            :base(fi, locationName)
        {
            
        }

        protected override void sendDataLine(List<String> lstData)
        {
            JMessage jMessage = new JMessage();
            jMessage.Function = MessageFunctions.Str_JEnterProductionDataLineRequest;
            jMessage.Sender = this.locationName;

            JEnterProductionDataLineRequest jEnterProductionDataLineRequest = new JEnterProductionDataLineRequest();
            jEnterProductionDataLineRequest.SourceFile = this.fiSource.Name;
            jEnterProductionDataLineRequest.LineNumber = lineCounter;
            jEnterProductionDataLineRequest.DataColumns = new List<JDataValue>();

            DataClient.logEntry(System.Diagnostics.EventLogEntryType.Information, "ProductionFile.sendDataLine begin for "
                                                                                  + this.fiSource.Name + " Line " + lineCounter);
            Console.WriteLine("ProductionFile.sendDataLine begin for " + this.fiSource.Name + " Line " + lineCounter);

            int iHeaderIndex = 0;

            // set the timestamp of the line, it is the first entry
            jEnterProductionDataLineRequest.dtCreated = DateTime.ParseExact(lstData[0].Replace("\"", ""), DateTimeFormat, System.Globalization.CultureInfo.InvariantCulture);

            // begin to iterate at the second position, first is the timestamp
            for(int i = 1; i < lstData.Count; i++)
            {
                jEnterProductionDataLineRequest.DataColumns.Add(new JDataValue() { Key = lstHeader[iHeaderIndex].Key, Value = lstData[i], Unity= lstHeader[iHeaderIndex].Unity, Device = lstHeader[iHeaderIndex].Device, Article = lstHeader[iHeaderIndex].Article });
                iHeaderIndex++;
            }

            jMessage.InnerMessage = Newtonsoft.Json.JsonConvert.SerializeObject(jEnterProductionDataLineRequest);

            String message = Newtonsoft.Json.JsonConvert.SerializeObject(jMessage);

            bool sendSuccess = false;

            String reply = DataManager.Network.TCPClient.sendMessage(message, "", out sendSuccess);

            DataClient.logEntry(System.Diagnostics.EventLogEntryType.SuccessAudit, "ProductionFile.sendDataLine success: " + this.fiSource.Name + " Line " + lineCounter);

            
            if (handleReply(reply))
            {
                DataClient.logEntry(System.Diagnostics.EventLogEntryType.SuccessAudit, "ProductionFile.sendDataLine success: " + this.fiSource.Name + " Line " + lineCounter);
                Console.WriteLine("ProductionFile.sendDataLine Success for " + this.fiSource.Name + " Line " + lineCounter);
            }
            

        }

        private bool handleReply(String strReply)
        {
            JMessage reply = null;
            JEnterDataLineReply dataReply = null;

            try
            {
                reply = Newtonsoft.Json.JsonConvert.DeserializeObject<JMessage>(strReply);
                dataReply = Newtonsoft.Json.JsonConvert.DeserializeObject<JEnterDataLineReply>(reply.InnerMessage);
            }
            catch (Exception ex)
            {
                DataClient.logEntry(System.Diagnostics.EventLogEntryType.Error, "ProductionFile.handleReply failed: " + ex.Message);
                throw new DataException.DataFormatException();
            }

            if(dataReply != null)
            {
                DataClient.logEntry(System.Diagnostics.EventLogEntryType.Information, "ProductionFile.handleReply: DataReply with error " 
                                                                                 + dataReply.ErrorCode + " " + dataReply.ErrorText);
                if (!dataReply.Success)
                {
                    Console.WriteLine("DataReply with error " + dataReply.ErrorCode + " " + dataReply.ErrorText);
                    handleDataReplyError(dataReply.ErrorCode);

                }
              
            }

            return true;

           
        }


    }
}
