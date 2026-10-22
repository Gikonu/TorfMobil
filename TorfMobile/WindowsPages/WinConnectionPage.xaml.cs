using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TorfMobile.ViewModels;

namespace TorfMobile
{
	public partial class WinConnectionPage : ContentPage
	{

		public WinConnectionPage(DeviceConnectionViewModel viewModel)
		{
			InitializeComponent();
			BindingContext = viewModel;
        }

    }
}