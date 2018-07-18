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
    public class ConsumptionFile : DataFile
    {

        public ConsumptionFile(FileInfo fi, String locationName)
            : base(fi, locationName)
        {
            
        }

        protected override void sendDataLine(List<String> lstData)
        {
            JMessage jMessage = new JMessage();
            jMessage.Function = MessageFunctions.Str_JEnterPowerUsageDataLineRequest;
            jMessage.Sender = this.locationName;

            JEnterPowerUsageDataLineRequest jEnterPowerUsageDataLineRequest = new JEnterPowerUsageDataLineRequest();
            jEnterPowerUsageDataLineRequest.SourceFile = this.fiSource.Name;
            jEnterPowerUsageDataLineRequest.LineNumber = lineCounter;
            jEnterPowerUsageDataLineRequest.DataColumns = new List<JDataValue>();

            DataClient.logEntry(System.Diagnostics.EventLogEntryType.Information, "ConsumptionFile.sendDataLine begin for "
                                                                                  + this.fiSource.Name + " Line " + lineCounter);
            Console.WriteLine("ConsumptionFile.sendDataLine begin for " + this.fiSource.Name + " Line " + lineCounter);

            int iHeaderIndex = 0;

            // set the timestamp of the line, it is the first entry
            jEnterPowerUsageDataLineRequest.dtCreated = DateTime.ParseExact(lstData[0].Replace("\"", ""), DateTimeFormat, System.Globalization.CultureInfo.InvariantCulture);

            // begin to iterate at the second position, first is the timestamp
            for (int i = 1; i < lstData.Count; i++)
            {
                jEnterPowerUsageDataLineRequest.DataColumns.Add(new JDataValue() { Key = lstHeader[iHeaderIndex].Key, Value = lstData[i], Unity = lstHeader[iHeaderIndex].Unity, Device = lstHeader[iHeaderIndex].Device, Article = lstHeader[iHeaderIndex].Article });
                iHeaderIndex++;
            }

            jMessage.InnerMessage = Newtonsoft.Json.JsonConvert.SerializeObject(jEnterPowerUsageDataLineRequest);

            String message = Newtonsoft.Json.JsonConvert.SerializeObject(jMessage);

            bool sendSuccess = false;

            String reply = DataManager.Network.TCPClient.sendMessage(message, "", out sendSuccess);

            if (handleReply(reply))
            {
                DataClient.logEntry(System.Diagnostics.EventLogEntryType.SuccessAudit, "ConsumptionFile.sendDataLine success: " + this.fiSource.Name + " Line " + lineCounter);
                Console.WriteLine("ConsumptionFile.sendDataLine Success for " + this.fiSource.Name + " Line " + lineCounter);
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
                DataClient.logEntry(System.Diagnostics.EventLogEntryType.Error, "ConsumptionFile.handleReply failed: " + ex.Message);
                throw new DataException.DataFormatException();
            }

            if (dataReply != null)
            {
                DataClient.logEntry(System.Diagnostics.EventLogEntryType.Information, "ConsumptionFile.handleReply: DataReply with error "
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
