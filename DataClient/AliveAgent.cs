using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataManager.Network.JSON;

namespace DataManagement.DataClientLib
{
    public class AliveAgent
    {

        private System.Timers.Timer tiAliveWorker;

        private int workerTimerTick;

        private String location;

        private object synchObject;

        private bool workerBusy;

        public int WorkerTimerTick
        {
            get { return workerTimerTick; }
            set { this.workerTimerTick = value; }
        }

        public AliveAgent(int agentTickMillis, String location)
        {
            workerTimerTick = agentTickMillis;
            this.location = location;

            synchObject = new object();
            workerBusy = false;

            tiAliveWorker = new System.Timers.Timer();
            tiAliveWorker.Elapsed += TiAliveWorker_Elapsed;
            tiAliveWorker.Interval = 20000;
        }

        public void start()
        {
            tiAliveWorker.Start();
        }

        public void stop()
        {
            tiAliveWorker.Stop();
        }

        private void TiAliveWorker_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (workerBusy)
                return;

            lock(synchObject)
            {
                try
                {
                    workerBusy = true;
                    tiAliveWorker.Interval = this.workerTimerTick;

                    setAliveMessage();
                    DataClient.logEntry(System.Diagnostics.EventLogEntryType.Information, "AliveAgent.sendMessage success");
                    Console.WriteLine("Alive Message for " + location + " success.");

                }
                catch(Exception ex)
                {
                    DataClient.logEntry(System.Diagnostics.EventLogEntryType.Error, "AliveAgent.sendMessage failed: " + ex.Message);
                    Console.WriteLine("Alive Message for " + location + " failed: " + ex.Message);
                }
                finally
                {
                    workerBusy = false;
                }

            }
        }

        private void setAliveMessage()
        {
            JMessage message = new JMessage();
            message.Sender = location;
            message.Function = "JSetAliveRequest";

            JSetAliveRequest jSetAliveRequest = new JSetAliveRequest();
            jSetAliveRequest.DateAlive = DateTime.Now;

            message.InnerMessage = Newtonsoft.Json.JsonConvert.SerializeObject(jSetAliveRequest);

            bool success = false;

            DataManager.Network.TCPClient.sendMessage(Newtonsoft.Json.JsonConvert.SerializeObject(message), "", out success);

            if (success)
                DataClient.logEntry(System.Diagnostics.EventLogEntryType.Information, "AliveAgent setAliveMessage success");
            else
                DataClient.logEntry(System.Diagnostics.EventLogEntryType.Information, "AliveAgent setAliveMessage failed");

        }

    }
}
