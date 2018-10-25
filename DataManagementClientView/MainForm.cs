using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DataManagement.DataClientLib;

namespace DataManagementClientView
{
    public partial class DataManagement : Form
    {
        public DataManagement()
        {
            InitializeComponent();

            // Start the data client
            DataClient client = new DataClient(Properties.Settings.Default.ConfigPath);
          
            client.startWorker();
           
        }
    }
}
