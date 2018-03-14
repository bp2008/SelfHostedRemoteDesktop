using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace MasterServer
{
	public partial class MasterServerSvc : ServiceBase
	{
		public MasterServerSvc()
		{
			InitializeComponent();
		}

		protected override void OnStart(string[] args)
		{
			ServiceWrapper.Initialize();
			ServiceWrapper.Start();
		}

		protected override void OnStop()
		{
			ServiceWrapper.Stop();
		}
	}
}
