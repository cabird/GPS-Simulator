﻿//
//  Created by Richard Zhang (Richard.Rupo.Zhang@gmail.com) on 3/2020
//  Copyright © 2020 Richard Zhang. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows.Forms;

namespace GPS_Simulator
{
    partial class dev_prov : Form
    {
        private DeviceModel device;
        public dev_prov(MainWindow mwnd, List<DeviceModel> Devices)
        {
            InitializeComponent();

            detailed_devinfo.ReadOnly = true;

            device = Devices[0];
            this.devinfo.Text = device.Name + "(" + device.Version + ")";

            if (device.isDevMode)
            {
                this.detailed_devinfo.Text = "Device is already properly provisioned!";
                this.start_prov_btn.Enabled = false;
            }
            else
            {
                this.detailed_devinfo.Text = "Device is NOT provisioned\n";
                this.start_prov_btn.Enabled = true;
            }
        }
        #region Assembly Attribute Accessors

        public string AssemblyTitle
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0)
                {
                    AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (titleAttribute.Title != "")
                    {
                        return titleAttribute.Title;
                    }
                }
                return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
            }
        }

        public string AssemblyVersion
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        public string AssemblyDescription
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyDescriptionAttribute)attributes[0]).Description;
            }
        }

        public string AssemblyProduct
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyProductAttribute)attributes[0]).Product;
            }
        }

        public string AssemblyCopyright
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
            }
        }

        public string AssemblyCompany
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCompanyAttribute)attributes[0]).Company;
            }
        }
        #endregion

        /// <summary>
        /// this is quick dirty method -- calling ideviceimagemounter.exe to
        /// mount the DeveloperDiskImage.dmg to device.
        /// A more sophisticated / robust way is needed to improve this.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void start_prov_btn_Click(object sender, EventArgs e)
        {
            // disable the button during the process to prevent re-entry
            this.start_prov_btn.Enabled = false;

            if (device.isDevMode)
            {
                System.Windows.Forms.MessageBox.Show("Device is already properly provisioned!");
                return;
            }
            this.detailed_devinfo.Text += "Device provisioning start...\n";

            // 1.download the DDI package.
            this.detailed_devinfo.Text += "1. Download provisioning package for your device....\n";
            string prov_pkg_url = device_utils.get_ddi_image_url(device);

            // make sure the temp directory exists.  If not, create it
            string tmpPath = Path.GetTempPath();
            if (!Directory.Exists(tmpPath))
            {
                this.detailed_devinfo.Text += $"Temp directory path {tmpPath} doesn't exist. Attempting to create it.";
                try
                {
                    Directory.CreateDirectory(tmpPath);
                } catch (Exception ex)
                {
                    this.detailed_devinfo.Text += $"Error creating directory {tmpPath}.  Please create it yourself and rerun.";
                    return;
                }
                
            }

            string local_package_file = Path.Combine(tmpPath, device.FullVersion + ".zip");
            try
            {
                WebClient webClient = new WebClient();
                webClient.Headers.Add("cookie", "");
                webClient.DownloadFile(new Uri(prov_pkg_url), local_package_file);
            }
            catch (Exception ex)
            {
                this.detailed_devinfo.Text += "we can't find" + device.FullVersion + ".zip from our repository," +
                    " please go to apple.com download the image and manually provision your device. \n";
                return;
            }

            this.detailed_devinfo.Text += "Done. \n";

            // 2. unzip the package.. 
            // FIXME: better to use async downloader.
            this.detailed_devinfo.Text += "2. Unzipping the package... \n";
            string local_folder = @"c:\temp\";

            // clear up the directory if it has anything there
            if (System.IO.Directory.Exists(local_folder + device.FullVersion))
            {
                System.IO.Directory.Delete(local_folder + device.FullVersion, true);
            }

            System.IO.Compression.ZipFile.ExtractToDirectory(local_package_file, local_folder);

            string dev_image_path = local_folder + device.FullVersion + @"\DeveloperDiskImage.dmg";

            bool all_files_present = (System.IO.File.Exists(dev_image_path) &&
                System.IO.File.Exists(dev_image_path + ".signature"));
            if (!all_files_present)
            {
                this.detailed_devinfo.Text += "Error: Package doesn't contain correct payload!\n";
                return;
            }
            this.detailed_devinfo.Text += "Done.\n";

            // 3. call the provisioning tool to do the job.
            this.detailed_devinfo.Text += "3. Provisioning...\n";
            string mounter = System.AppDomain.CurrentDomain.BaseDirectory +
                @"win-x86\ideviceimagemounter.exe";

            if (!System.IO.File.Exists(mounter))
            {
                this.detailed_devinfo.Text += "Error: image mounter is not found!\n";
                return;
            }

            System.Diagnostics.Process p = new System.Diagnostics.Process();
            p.StartInfo.FileName = mounter;
            p.StartInfo.Arguments = dev_image_path;

            // Do not show the console window.
            p.StartInfo.WindowStyle  = System.Diagnostics.ProcessWindowStyle.Hidden;
            p.Start();
            p.WaitForExit();

            if (device_utils.is_device_on_dev_mode(device.UDID))
            {
                this.detailed_devinfo.Text += "Done.\n";
                this.detailed_devinfo.Text += "Congratulations, your device is provisioned successfully, " +
                    "please reconnect the USB cable to your PC.\n";
            }
            else
            {
                this.detailed_devinfo.Text += "Failed.\n";
                this.detailed_devinfo.Text += "Please ensure the device screen is NOT locked!\n";
                this.start_prov_btn.Enabled = true;
            }

            // enable the button back.
            this.start_prov_btn.Enabled = true;
        }
    }
}
