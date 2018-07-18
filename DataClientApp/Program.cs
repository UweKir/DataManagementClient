using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataManager.Network;
using DataManager.Network.JSON;
using DataManagement.DataClientLib;
using System.IO;

using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

using System.Text.RegularExpressions;


using Newtonsoft.Json;


namespace DataClientApp
{
    class Program
    {
        static void Main(string[] args)
        {
          


            DataClient client = new DataClient(@"C:\KTBDataManager\Programs\Config\KTBDataManagerClient.ini");
            //DataClient client2 = new DataClient(@"C:\KTBDataManager\Programs\Config\KTBDataManagerClient2.ini");
            client.startWorker();
            //client2.startWorker();

            Console.ReadLine();

           
            
        }

        

    }
}
