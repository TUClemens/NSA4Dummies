﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpPcap;
using SharpPcap.LibPcap;
using SharpPcap.AirPcap;
using SharpPcap.WinPcap;
using PacketDotNet;
using System.ComponentModel;
using System.Threading;

namespace Softwareprojekt2015
{
    class PacketSniffer
    {

        // Retrieves the list of network devices.
        private LibPcapLiveDeviceList deviceList;

        // EventWaitHandle to let thread react on event
        private EventWaitHandle ewh;

        // Create the FileWriterDevice to write to a pcap file.
        private CaptureFileWriterDevice captureFileWriter;

        DataPacket currentPacket;


        public PacketSniffer()
        {

            deviceList = LibPcapLiveDeviceList.Instance;

            

            

            foreach (var adapter in deviceList)
            {

                // Register the handler function to the packet arrival event.
                adapter.OnPacketArrival += new PacketArrivalEventHandler(device_OnPacketArrival);

                //captureFileWriter = new CaptureFileWriterDevice(device, "test.pcpap");

                // Open the device for capturing
                int readTimeoutMilliseconds = 1000;

                


                if (adapter is AirPcapDevice)
                {
                    var airPcap = adapter as AirPcapDevice;
                    airPcap.Open(SharpPcap.WinPcap.OpenFlags.DataTransferUdp, readTimeoutMilliseconds);
                }
                else if (adapter is WinPcapDevice)
                {
                    var winPcap = adapter as WinPcapDevice;
                    winPcap.Open(SharpPcap.WinPcap.OpenFlags.DataTransferUdp | SharpPcap.WinPcap.OpenFlags.NoCaptureLocal, readTimeoutMilliseconds);
                }
                else if (adapter is LibPcapLiveDevice)
                {
                    var livePcapDevice = adapter as LibPcapLiveDevice;
                    livePcapDevice.Open(DeviceMode.Promiscuous, readTimeoutMilliseconds);
                }
                else
                {
                    throw new System.InvalidOperationException("unknown device type of " + adapter.GetType().ToString());
                }

                string filter = "ip and tcp";
                adapter.Filter = filter;

                // Start the capturing process
                adapter.StartCapture();
            }

            ewh = new EventWaitHandle(false, EventResetMode.AutoReset);

        }

        public void KillSniffer()
        {
            foreach (var device in deviceList)
            {
                device.StopCapture();
                device.Close();
            }
        }


        public void RunPacketSniffer(object sender, DoWorkEventArgs e)
        {

            while (!e.Cancel)
            {

                ewh.WaitOne();

                if (((App)App.Current).snifferWorker.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }

                ((App)App.Current).snifferWorker.ReportProgress(0, currentPacket);

            }


        }

        private static int packetIndex = 0;

        private void device_OnPacketArrival(object sender, CaptureEventArgs e)
        {
            //captureFileWriter.Write(e.Packet);
            currentPacket = new DataPacket();

            currentPacket.Data = e.Packet.Data;
            currentPacket.Length = e.Packet.Data.Length;
            currentPacket.Time = e.Packet.Timeval.Date;

            var packet = PacketDotNet.Packet.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data);
            var tcpPacket = PacketDotNet.TcpPacket.GetEncapsulated(packet);
            if (tcpPacket != null)
            {
                

                IpPacket ipPacket = (IpPacket)tcpPacket.ParentPacket;


                currentPacket.DestIP = ipPacket.DestinationAddress;
                currentPacket.SourceIP = ipPacket.SourceAddress;

            }




            ewh.Set();
        }

        public void Cancel()
        {
            
            ((App)App.Current).snifferWorker.CancelAsync();
            ewh.Set();

            KillSniffer();
            
        }
    }

}