//
//  Created by Richard Zhang (Richard.Rupo.Zhang@gmail.com) on 3/2020
//  Copyright © 2020 Richard Zhang. All rights reserved.
//
// libimobiledevice-net references
using iMobileDevice;
// Bing Map WPF control
using Microsoft.Maps.MapControl.WPF;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
//using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;

using System.Net.Http;
using System.IO;
using System.Threading.Tasks;
using Geo;
using Geo.Gps.Serialization;
using Geo.Gps;
using System.Runtime.Remoting.Channels;
using System.Diagnostics;
using System.Windows.Media;
using System.Web;

namespace GPS_Simulator
{
    public class list_item
    {
        public Location loc { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // fast walking speed is at 8.8km/h == 2.47 m/s
        const double c_fast_walking_speed = 2.47f;

        // Current query result
        public List<list_item> g_query_result = new List<list_item>();
        LocationCollection route;

        private static DispatcherTimer walkingTimer = null;
        
        LocationService locService = null;
        public Pushpin teleportPin = null;

        private Navigator navigator;
        private MapUIManager uiMgr;
        public string BingMapKey = @"MRoghxvRwiH04GVvGpg4~uaP_it5CCQ6ckz-j9tA_iQ~AoPUZFQPIn9s1qjKPLgkvgeGPZPKznUlqM_e0fPu8NCXTi_ZSZTDud4_j0F1SkKU";
        private WebServices webServices;

        /// <summary>
        /// main window initialization
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // default is walking
            walking_speed.IsChecked = true;
            running_speed.IsChecked = false;
            driving_speed.IsChecked = false;

            // load native libraries for iDevice
            NativeLibraries.Load();

            // init walking timer.
            walkingTimer = new DispatcherTimer();
            walkingTimer.Interval = TimeSpan.FromMilliseconds(500); // 0.5 sec
            walkingTimer.Tick += DoOneStep;
            walkingTimer.IsEnabled = true;

            navigator = new Navigator();
            navigator.walkingSpeed = c_fast_walking_speed;
            navigator.WalkingState = WalkingState.Stopped;

            uiMgr = new MapUIManager(myMap, this);
            webServices = new WebServices(BingMapKey);

            route = new LocationCollection();
            uiMgr.SetRoute(route);
            navigator.SetRoute(route);

            locService = LocationService.GetInstance(this);
            locService.ListeningDevice();

            if (locService.Devices.Count < 1)
            {
                device_prov.IsEnabled = false;
            }

            walkingTimer.Start();
        }


        public void DoOneStep(Object sender, EventArgs e)
        {
            Location location = navigator.GetNextStepLocation(gps_drift.IsChecked.Value);
            if (location == null) return;
            uiMgr.SetCurrentPushpinLocation(location);
            // update the location to device
            locService.UpdateLocation(location);
        }

        /// <summary>
        /// set speeds.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void walking_speed_click(object sender, RoutedEventArgs e)
        {
            navigator.walkingSpeed = c_fast_walking_speed;
        }

        private void driving_speed_click(object sender, RoutedEventArgs e)
        {
            navigator.walkingSpeed = c_fast_walking_speed * 12;
        }

        private void running_speed_click(object sender, RoutedEventArgs e)
        {
            navigator.walkingSpeed = c_fast_walking_speed * 3;
        }



        /// <summary>
        ///  start to walk and auto repeat.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void walk_Button_Click(object sender, RoutedEventArgs e)
        {
           
            switch (navigator.WalkingState)
            {
                case WalkingState.Stopped:
                    // stopped -- > active
                    walking.Content = "Pause"; // indicate use can pause in active.
                    load_gpx_button.IsEnabled = false;
                    navigator.WalkingState = WalkingState.Active;
                    break;

                case WalkingState.Paused:
                    // paused -- > active
                    walking.Content = "Pause"; // indicate use can pause in active.
                    load_gpx_button.IsEnabled = false;
                    navigator.WalkingState = WalkingState.Active;
                    break;

                case WalkingState.Active:
                    // active --> paused
                    walking.Content = "Resume"; // indicate use can resume in paused.
                    load_gpx_button.IsEnabled = true;
                    navigator.WalkingState = WalkingState.Paused;
                    break;

                default: break;
            }
        }

        /// <summary>
        /// load GPX
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void load_gpx_button_Click(object sender, RoutedEventArgs e)
        {
            if (GpxUtils.OpenGpxDlg(out string gpxFileName) == System.Windows.Forms.DialogResult.Cancel)
            {
                return;
            }
            if (gpxFileName == null || gpxFileName.Length == 0)
            {
                return;
            }

            var newRoute = GpxUtils.ReadGpxCoords(gpxFileName);

            route.Clear();
            foreach (Location loc in newRoute) route.Add(loc);

            navigator.SetRoute(route);
            uiMgr.SetRoute(route);
            myMap.Center = route[0];
        }

        /// <summary>
        ///  teleport to a location.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tele_Button_Click(object sender, RoutedEventArgs e)
        {
            if (navigator.WalkingState != WalkingState.Stopped)
            {
                System.Windows.Forms.MessageBox.Show("Quit from walking mode first.");
                return;
            }

            Location tele = new Location();

            try
            {
                tele.Latitude = Convert.ToDouble(lat.Text);
                tele.Longitude = Convert.ToDouble(lon.Text);
                tele.Altitude = Convert.ToDouble(alt.Text);
            }
            catch
            {
                System.Windows.Forms.MessageBox.Show("the GPS coordination is invalid!");
                return;
            }

            TeleportToLocation(tele);
        }


    
        private void WalkToLocation(Location location)
        {
            if (teleportPin != null)
            {
                myMap.Children.Remove(teleportPin);
            }
            route.Clear();
            route.Add(navigator.CurrentLocation);
            route.Add(location);
            walking_mode_label.Content = "Walking Mode: Towards Point";
            walking.Content = "Pause"; // indicate use can pause in active.
            uiMgr.SetRoute(route);
            navigator.SetRoute(route);
            navigator.StartWalkingRoute();
            //navigator.WalkToLocation(location);
        }

        private void WalkToLocationByDirections(Location targetLocation)
        {
            if (teleportPin != null)
            {
                myMap.Children.Remove(teleportPin);
            }
            walking_mode_label.Content = "Walking Mode: Towards Point By Directions";
            walking.Content = "Pause";

            webServices.GetDirectionsWaypointList(navigator.CurrentLocation, targetLocation, out List<Location> waypoints, out List<Location> snappedRoute);
            route.Clear();
            foreach (Location loc in snappedRoute) route.Add(loc);
            var waypointLocationCollection = new LocationCollection();
            foreach (Location loc in waypoints) waypointLocationCollection.Add(loc);
            uiMgr.SetRoute(waypointLocationCollection, route);
            navigator.SetRoute(route);
            navigator.StartWalkingRoute();
        }

        private void TeleportToLocation(Location location)
        { 
            // The pushpin to add to the map.
            if (teleportPin != null)
            {
                myMap.Children.Remove(teleportPin);
            }
            else
            {
                teleportPin = new Pushpin();
            }

            location.Altitude = webServices.GetElevationForLocation(location);
            teleportPin.Location = location;

            // Adds the pushpin to the map.
            myMap.Children.Add(teleportPin);

            // update the coords
            lat.Text = location.Latitude.ToString();
            lon.Text = location.Longitude.ToString();
            alt.Text = location.Altitude.ToString();

            locService.UpdateLocation(location);
            navigator.TeleportToLocation(location);
            uiMgr.SetCurrentPushpinLocation(location);

            myMap.Center = location;
        }

        private void device_Button_Click(object sender, RoutedEventArgs e)
        {
            // only take care of the first device.
            if (LocationService.GetInstance(this).Devices.Count > 1)
            {
                System.Windows.Forms.MessageBox.Show("More than one device is connected, provision tool only support one device at a time!");
                return;
            }

            dev_prov dlg = new dev_prov(this, LocationService.GetInstance(this).Devices);
            dlg.ShowDialog();
        }

        private void stop_Button_Click(object sender, RoutedEventArgs e)
        {
            switch (navigator.WalkingState)
            {
                case WalkingState.Stopped:
                    // do nothing. it is already stopped.
                    break;

                case WalkingState.Active:
                case WalkingState.Paused:
                    // reset the current position to the beginning.
                    walking.Content = "Start"; // indicate use can start in stopped.
                    load_gpx_button.IsEnabled = true; // user can load new route.

                    // reset to the beginning of the route.
                    navigator.RoutingMode = RoutingMode.Stopped;
                    navigator.WalkingState = WalkingState.Stopped;
                    break;

                default: break;
            }
        }

        // TODO - the majority of this body that handles parsing should be moved to WebServices...
        private void search_Button_Click(object sender, RoutedEventArgs e)
        {
            search_result_list.Items.Clear();
            g_query_result.Clear();

            if (search_box.Text.Length <= 0)
            {
                return;
            }

            // Make the request and get the response
            XmlDocument geocodeResponse = webServices.DoBingLocationSearch(search_box.Text);

            // Create namespace manager
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(geocodeResponse.NameTable);
            nsmgr.AddNamespace("rest", "http://schemas.microsoft.com/search/local/ws/rest/v1");

            // Get all geocode locations in the response 
            XmlNodeList locationElements = geocodeResponse.SelectNodes("//rest:Location", nsmgr);

            if (locationElements.Count <= 0)
            {
                return;
            }

            for (int i = 0; i < locationElements.Count; i++)
            {
                Location loc = new Location();
                loc.Latitude = Convert.ToDouble(locationElements[i].SelectSingleNode(".//rest:Latitude", nsmgr).InnerText);
                loc.Longitude = Convert.ToDouble(locationElements[i].SelectSingleNode(".//rest:Longitude", nsmgr).InnerText);

                list_item it = new list_item();
                it.loc = loc;
                it.Name = locationElements[i].SelectSingleNode(".//rest:Name", nsmgr).InnerText;

                g_query_result.Add(it);
                search_result_list.Items.Add(it.Name);
            }

            // get the elevations for these addresses.
            List<double> alt_list = webServices.GetElevationsForLocations(g_query_result.Select(item => item.loc));

            for (int i = 0; i < g_query_result.Count; i++)
            {
                g_query_result[i].loc.Altitude = alt_list[i];
            }
        }

        private void search_result_list_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (g_query_result.Count <= 0 || search_result_list.SelectedIndex < 0)
            {
                return;
            }
            else
            {
                list_item it = g_query_result[search_result_list.SelectedIndex];
                lat.Text = it.loc.Latitude.ToString();
                lon.Text = it.loc.Longitude.ToString();
                alt.Text = it.loc.Altitude.ToString();
            }
        }

        private void SaveGpxButton_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (GpxUtils.SaveGpxDialog(out string gpxFileName) == System.Windows.Forms.DialogResult.OK)
            {
                GpxUtils.SaveGpxCoordinates(route, gpxFileName);
            }
        }

        private void WalkHereMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var control = sender as Control;
            var location = control.DataContext as Location;
            WalkToLocation(location);
        }

        private void TeleportHereMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var control = sender as Control;
            var location = control.DataContext as Location;
            TeleportToLocation(location);
        }

        private void WalkHereByDirectionsMenutItem_Click(object sender, RoutedEventArgs e)
        {
            var control = sender as Control;
            var location = control.DataContext as Location;
            WalkToLocationByDirections(location);
        }

        private void walk_route_button_Click(object sender, RoutedEventArgs e)
        {
            //webServices.GetDirectionsWaypointList(null, null);

            navigator.SetRoute(route);
            navigator.StartWalkingRoute();

            // stopped -- > active
            walking.Content = "Pause"; // indicate use can pause in active.
            load_gpx_button.IsEnabled = false;
            walking_mode_label.Content = "Walking Mode: On Route";
        }

        // commands routed to the MapUIManager
        private void AddPinBeforeMenuItem_Click(object sender, RoutedEventArgs e) => uiMgr.AddPinBeforeMenuItem_Click(sender, e);
        private void AddPinAfterMenuItem_Click(object sender, RoutedEventArgs e) => uiMgr.AddPinAfterMenuItem_Click(sender, e);
        private void RemovePinMenuItem_Click(object sender, RoutedEventArgs e) => uiMgr.RemovePinMenuItem_Click(sender, e);

       
    }
}
