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
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;

using System.Net.Http;
using System.IO;
using System.Threading.Tasks;
using Geo;
using Geo.Gps.Serialization;
using Geo.Gps;

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

        public enum e_walking_state
        {
            walking_active = 1,
            walking_paused = 2,
            walking_stopped = 3
        }

        public e_walking_state cur_walking_state = e_walking_state.walking_stopped;

        public static string g_gpx_file_name = null;
        public MapPolyline g_polyline = null;
        private static DispatcherTimer walking_timer = null;
        private static walking_timer_callback timer_callback = null;
        location_service loc_service = null;
        public Pushpin teleport_pin = null;

        public string BingMapKey = @"MRoghxvRwiH04GVvGpg4~uaP_it5CCQ6ckz-j9tA_iQ~AoPUZFQPIn9s1qjKPLgkvgeGPZPKznUlqM_e0fPu8NCXTi_ZSZTDud4_j0F1SkKU";

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
            walking_timer = new DispatcherTimer();
            walking_timer.Interval = TimeSpan.FromMilliseconds(500); // 0.5 sec
            timer_callback = new walking_timer_callback(g_polyline, myMap, this);
            timer_callback.walking_speed = c_fast_walking_speed;
            walking_timer.Tick += timer_callback.one_step;
            walking_timer.IsEnabled = true;
            walking_timer.Stop();

            loc_service = location_service.GetInstance(this);
            loc_service.ListeningDevice();

            if (loc_service.Devices.Count < 1)
            {
                device_prov.IsEnabled = false;
            }
        }
        /// <summary>
        ///  load GPX track files.
        /// </summary>
        public void OptionDlg()
        {
            OpenFileDialog gpx_open_dlg = new OpenFileDialog
            {
                InitialDirectory = @"D:\",
                Title = "Browse GPX Files",

                CheckFileExists = true,
                CheckPathExists = true,

                DefaultExt = "GPX",
                Filter = "GPX files (*.gpx)|*.gpx",
                FilterIndex = 2,
                RestoreDirectory = true,

                ReadOnlyChecked = true,
                ShowReadOnly = true
            };

            var result = gpx_open_dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                if (g_gpx_file_name != gpx_open_dlg.FileName
                    && gpx_open_dlg.FileName != null)
                {
                    // clear the previous route
                    myMap.Children.Remove(g_polyline);

                    g_gpx_file_name = gpx_open_dlg.FileName;

                    // draw route
                    draw_gpx_route();
                }
            }

        }

        private static void read_gpx_coords(string gpx_file_name, ref LocationCollection lc)
        {
            XDocument gpx_file = XDocument.Load(gpx_file_name);
            XNamespace gpx = XNamespace.Get("http://www.topografix.com/GPX/1/1");

            var waypoints = from waypoint in gpx_file.Descendants(gpx + "wpt")
                            select new
                            {
                                Latitude = waypoint.Attribute("lat").Value,
                                Longitude = waypoint.Attribute("lon").Value,
                                Elevation = waypoint.Element(gpx + "ele") != null ? waypoint.Element(gpx + "ele").Value : null
                            };

            foreach (var wpt in waypoints)
            {
                lc.Add(new Location(Convert.ToDouble(wpt.Latitude),
                    Convert.ToDouble(wpt.Longitude),
                    Convert.ToDouble(wpt.Elevation)));
            }

            var tracks = from track in gpx_file.Descendants(gpx + "trk")
                         select new
                         {
                             Name = track.Element(gpx + "name") != null ? track.Element(gpx + "name").Value : null,
                             Segs = (
                             from trackpoint in track.Descendants(gpx + "trkpt")
                             select new
                             {
                                 Latitude = trackpoint.Attribute("lat").Value,
                                 Longitude = trackpoint.Attribute("lon").Value,
                                 Elevation = trackpoint.Element(gpx + "ele") != null ? trackpoint.Element(gpx + "ele").Value : null
                             }
                             )
                         };

            foreach (var trk in tracks)
            {
                // Populate track data.
                foreach (var trkSeg in trk.Segs)
                {
                    lc.Add(new Location(Convert.ToDouble(trkSeg.Latitude),
                        Convert.ToDouble(trkSeg.Longitude),
                        Convert.ToDouble(trkSeg.Elevation)));
                }

            }
        }
        /// <summary>
        /// Draw GPX tracks on the map.
        /// </summary>
        public void draw_gpx_route()
        {
            if (g_gpx_file_name == null
                || g_gpx_file_name.Length == 0)
            {
                return;
            }

            LocationCollection lc = new LocationCollection();
            read_gpx_coords(g_gpx_file_name, ref lc);

            // draw the tack on map
            MapPolyline polyline = new MapPolyline();
            polyline.Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.
                Colors.Blue);

            polyline.StrokeThickness = 3;
            polyline.Opacity = 0.7;

            polyline.Locations = lc;

            myMap.Children.Add(polyline);

            g_polyline = polyline;

            myMap.Center = lc[0];

            // set the walking to the beginning of new route
            if (timer_callback != null)
            {
                timer_callback.set_route(g_polyline);
                cur_walking_state = e_walking_state.walking_stopped;
                walking.Content = "Start";
            }
        }

        /// <summary>
        /// set speeds.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void walking_speed_click(object sender, RoutedEventArgs e)
        {
            timer_callback.walking_speed = c_fast_walking_speed;
        }

        private void driving_speed_click(object sender, RoutedEventArgs e)
        {
            timer_callback.walking_speed = c_fast_walking_speed * 12;
        }

        private void running_speed_click(object sender, RoutedEventArgs e)
        {
            timer_callback.walking_speed = c_fast_walking_speed * 3;
        }

        private Location GetLocationForMousePosition(Point mousePosition)
        {
            var point = myMap.TransformToAncestor(this).Transform(new Point(0, 0));
            mousePosition.Offset(-point.X, -point.Y);

            Location location = myMap.ViewportPointToLocation(mousePosition);
            string elevationUrl = spell_elevation_query_url(location);
            List<double> elevations = get_elevations(elevationUrl);
            if (elevations.Count > 0)
            {
                location.Altitude = elevations[0];
            }

            return location;
        }

        private void walk_to_location(object sender, MouseButtonEventArgs e)
        {

            e.Handled = true;

            Point mousePosition = e.GetPosition(this);
            MapPolyline polyline = new MapPolyline();

            Location mouseLocation = GetLocationForMousePosition(e.GetPosition(this));

            if (location_service.LastKnownLocation == null)
            {
                TeleportToLocation(mouseLocation);
                return;
            }

            polyline.Locations = new LocationCollection();

            polyline.Locations.Add(location_service.LastKnownLocation);
            
                      
            // Determine the location to place the pushpin at on the map.

            // Get the mouse click coordinates

            // The pushpin to add to the map.
            if (teleport_pin != null)
            {
                myMap.Children.Remove(teleport_pin);
            }
            else
            {
                teleport_pin = new Pushpin();
            }

            teleport_pin.Location = mouseLocation;

            // Adds the pushpin to the map.
            myMap.Children.Add(teleport_pin);

            // update the coords
            lat.Text = mouseLocation.Latitude.ToString();
            lon.Text = mouseLocation.Longitude.ToString();
            alt.Text = mouseLocation.Altitude.ToString();
            // add the end location:
            polyline.Locations.Add(teleport_pin.Location);


            // initialize the timer call back
            if (timer_callback == null)
            {
                timer_callback = new walking_timer_callback(polyline, myMap, this);
            }

            timer_callback.set_route(polyline);
            walking_timer.Start();
        }

        /// <summary>
        ///  start to walk and auto repeat.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void walk_Button_Click(object sender, RoutedEventArgs e)
        {
            /*if (g_gpx_file_name == null)
            {
                System.Windows.Forms.MessageBox.Show("Please load a GPX file and then walk.");
                return;
            }*/

            // initialize the timer call back
            if (timer_callback == null)
            {
                timer_callback = new walking_timer_callback(polyline, myMap, this);
            }

            if (timer_callback.m_polyline == null)
            {
                timer_callback.set_route(polyline);
            }

            switch (cur_walking_state)
            {
                case e_walking_state.walking_stopped:
                    // stopped -- > active
                    walking.Content = "Pause"; // indicate use can pause in active.
                    walking_timer.Start();
                    option.IsEnabled = false;
                    cur_walking_state = e_walking_state.walking_active;
                    break;

                case e_walking_state.walking_paused:
                    // paused -- > active
                    walking.Content = "Pause"; // indicate use can pause in active.
                    walking_timer.Start();
                    option.IsEnabled = false;
                    cur_walking_state = e_walking_state.walking_active;
                    break;

                case e_walking_state.walking_active:
                    // active --> paused
                    walking.Content = "Resume"; // indicate use can resume in paused.
                    walking_timer.Stop();
                    option.IsEnabled = true;
                    cur_walking_state = e_walking_state.walking_paused;
                    break;

                default: break;
            }
        }

        /// <summary>
        /// load GPX
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void option_Button_Click(object sender, RoutedEventArgs e)
        {
            OptionDlg();
        }

        /// <summary>
        ///  teleport to a location.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tele_Button_Click(object sender, RoutedEventArgs e)
        {


            if (cur_walking_state != e_walking_state.walking_stopped)
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

            // The pushpin to add to the map.
            if (teleport_pin != null)
            {
                myMap.Children.Remove(teleport_pin);
            }
            else
            {
                teleport_pin = new Pushpin();
            }

            teleport_pin.Location = tele;

            // Adds the pushpin to the map.
            myMap.Center = tele;
            myMap.Children.Add(teleport_pin);

            location_service.GetInstance(this).UpdateLocation(tele);
        }

        List<Location> locations;
        List<Pushpin> pins;
        MapPolyline polyline;

        private void AddWayPoint(Location location)
        {
            if (locations == null)
            {
                locations = new List<Location>();
            }

            locations.Add(location);
        }

        private void DrawWayPoints()
        {
            ClearWayPoints();

            LocationCollection lc = new LocationCollection();
            
            if (pins == null)
            {
                pins = new List<Pushpin>();
            }

            foreach (var loc in locations)
            {
                lc.Add(loc);
                var pin = new Pushpin();
                pin.Location = loc;

                pin.MouseDown += Pin_MouseDown;
                pins.Add(pin);

                pin.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Colors.ForestGreen);
                myMap.Children.Add(pin);

            }

            // draw the tack on map
            polyline = new MapPolyline();
            polyline.Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.
                Colors.ForestGreen);

            polyline.StrokeThickness = 3;
            polyline.Opacity = 0.7;

            polyline.Locations = lc;

            myMap.Children.Add(polyline);

        }

        /* 
        //dragging pushpin from SO 
        Vector _mouseToMarker;
        private bool _dragPin;
        public Pushpin SelectedPushpin { get; set; }

        void pin_MouseDown(object sender, MouseButtonEventArgs e)
        {
          e.Handled = true;
          SelectedPushpin = sender as Pushpin;
          _dragPin = true;
          _mouseToMarker = Point.Subtract(
            map.LocationToViewportPoint(SelectedPushpin.Location), 
            e.GetPosition(map));
        }

        private void map_MouseMove(object sender, MouseEventArgs e)
        {
          if (e.LeftButton == MouseButtonState.Pressed)
          {
            if (_dragPin && SelectedPushpin != null)
            {
              SelectedPushpin.Location = map.ViewportPointToLocation(
                Point.Add(e.GetPosition(map), _mouseToMarker));
              e.Handled = true;
            }
          }
        }  
        */

        Pushpin selectedPushpin;
        Location oldLocation;
        bool _dragPin = false;
        Vector _mouseToMarker;

        private void Pin_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            // we only care about this event on a pushpin
            var pushpin = sender as Pushpin;
            if (pushpin == null) return;

            // we only care about right button click
            if (e.RightButton == MouseButtonState.Pressed && e.LeftButton == MouseButtonState.Released)
            { 
                //remove the pushpin and redraw the route.
                locations = locations.Where(loc => loc != pushpin.Location).ToList();
                DrawWayPoints();
            }

            if (e.LeftButton == MouseButtonState.Pressed && e.RightButton == MouseButtonState.Released)
            {
                selectedPushpin = pushpin;
                _dragPin = true;
                oldLocation = selectedPushpin.Location;
                _mouseToMarker = Point.Subtract(
                  myMap.LocationToViewportPoint(selectedPushpin.Location),
                  e.GetPosition(myMap));
            }
        }

        private void map_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                e.Handled = true;
                if (_dragPin && selectedPushpin != null)
                {
                    selectedPushpin.Location = myMap.ViewportPointToLocation(
                      Point.Add(e.GetPosition(myMap), _mouseToMarker));
                    e.Handled = true;

                    
                }
                
            }
        }

        private void map_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragPin && selectedPushpin != null)
            {
                _dragPin = false;
                locations = locations.Select(loc => loc == oldLocation ? selectedPushpin.Location : loc).ToList();
                DrawWayPoints();
            }
        }

        private void ClearWayPoints()
        {
            if (polyline != null)
                myMap.Children.Remove(polyline);
            if (pins != null)
            {
                foreach (var pin in pins)
                {
                    myMap.Children.Remove(pin);
                }
            }
            pins = new List<Pushpin>();
            
        }

        

        /// <summary>
        /// double click and teleport.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Map_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // TODO - CAB
            //walk_to_location(sender, e);

            var loc = GetLocationForMousePosition(e.GetPosition(this));
            AddWayPoint(loc);
            DrawWayPoints();
            e.Handled = true;
            return;

            // Disables double-click teleport when it is in walking mode.
            if (cur_walking_state == e_walking_state.walking_active)
            {
                System.Windows.Forms.MessageBox.Show("Quit from walking mode first.");
                return;
            }

            // Disables the default mouse double-click action.
            e.Handled = true;

            // Determine the location to place the pushpin at on the map.

            // Get the mouse click coordinates
            Point mousePosition = e.GetPosition(this);

            // WARNING:
            // It seems to be a bug of Bing Map WPF control, that when the control is 
            // not in full screen mode, the coords calculation got some offsets. 
            // make a dirty adjustment here.
            mousePosition.Offset(-Width * 3 / 16, 0);

            // Convert the mouse coordinates to a location on the map
            Location location = myMap.ViewportPointToLocation(mousePosition);
            TeleportToLocation(location);

        }

        private void TeleportToLocation(Location location)
        { 
            // The pushpin to add to the map.
            if (teleport_pin != null)
            {
                myMap.Children.Remove(teleport_pin);
            }
            else
            {
                teleport_pin = new Pushpin();
            }

            string elevationUrl = spell_elevation_query_url(location);
            List<double> elevations = get_elevations(elevationUrl);
            if (elevations.Count > 0)
            {
                location.Altitude = elevations[0];
            }

            teleport_pin.Location = location;

            // Adds the pushpin to the map.
            myMap.Children.Add(teleport_pin);

            // update the coords
            lat.Text = location.Latitude.ToString();
            lon.Text = location.Longitude.ToString();
            alt.Text = location.Altitude.ToString();

            location_service.GetInstance(this).UpdateLocation(location);
        }

        private void device_Button_Click(object sender, RoutedEventArgs e)
        {
            // only take care of the first device.
            if (location_service.GetInstance(this).Devices.Count > 1)
            {
                System.Windows.Forms.MessageBox.Show("More than one device is connected, provision tool only support one device at a time!");
                return;
            }

            dev_prov dlg = new dev_prov(this, location_service.GetInstance(this).Devices);
            dlg.ShowDialog();
        }

        private void stop_Button_Click(object sender, RoutedEventArgs e)
        {
            switch (cur_walking_state)
            {
                case e_walking_state.walking_stopped:
                    // do nothing. it is already stopped.
                    break;

                case e_walking_state.walking_active:
                case e_walking_state.walking_paused:
                    // reset the current position to the beginning.
                    walking.Content = "Start"; // indicate use can start in stopped.
                    walking_timer.Stop();
                    option.IsEnabled = true; // user can load new route.

                    // reset to the beginning of the route.
                    timer_callback.reset();
                    cur_walking_state = e_walking_state.walking_stopped;
                    break;

                default: break;
            }
        }

        private XmlDocument GetXmlResponse(string requestUrl)
        {
            HttpWebRequest request = WebRequest.Create(requestUrl) as HttpWebRequest;
            using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
            {
                if (response.StatusCode != HttpStatusCode.OK)
                    throw new Exception(String.Format("Server error (HTTP {0}: {1}).",
                    response.StatusCode,
                    response.StatusDescription));
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(response.GetResponseStream());
                return xmlDoc;
            }
        }

        // Format the URI from a list of locations.
        protected string spell_elevation_query_url(List<list_item> locList)
        {
            // The base URI string. Fill in: 
            // {0}: The lat/lon list, comma separated. 
            // {1}: The key. 
            const string BASE_URI_STRING =
              "http://dev.virtualearth.net/REST/v1/Elevation/List?points={0}&key={1}&o=xml";

            string retVal = string.Empty;
            string locString = string.Empty;
            for (int ndx = 0; ndx < locList.Count; ++ndx)
            {
                if (ndx != 0)
                {
                    locString += ",";
                }
                locString += locList[ndx].loc.Latitude.ToString() + "," + locList[ndx].loc.Longitude.ToString();
            }
            retVal = string.Format(BASE_URI_STRING, locString, BingMapKey);
            return retVal;
        }

        // spell the url for single point.
        protected string spell_elevation_query_url(Location loc)
        {
            // The base URI string. Fill in: 
            // {0}: The lat/lon list, comma separated. 
            // {1}: The key. 
            const string BASE_URI_STRING =
              "http://dev.virtualearth.net/REST/v1/Elevation/List?points={0}&key={1}&o=xml";

            string locString = loc.Latitude.ToString() + "," + loc.Longitude.ToString();
            return string.Format(BASE_URI_STRING, locString, BingMapKey);
            
        }

        protected List<double> get_elevations(string url)
        {
            List<double> ret = new List<double>();
            XmlDocument res = GetXmlResponse(url);
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(res.NameTable);
            nsmgr.AddNamespace("rest", "http://schemas.microsoft.com/search/local/ws/rest/v1");

            XmlNode elevationSets = res.SelectSingleNode("//rest:Elevations", nsmgr);
            foreach (XmlNode node in elevationSets.ChildNodes)
            {
                ret.Add(Convert.ToDouble(node.InnerText));
            }

            return ret;
        }

        private void search_Button_Click(object sender, RoutedEventArgs e)
        {
            search_result_list.Items.Clear();
            g_query_result.Clear();

            if (search_box.Text.Length <= 0)
            {
                return;
            }

            string requestUrl = @"http://dev.virtualearth.net/REST/v1/Locations/" + search_box.Text + "?o=xml&key=" + BingMapKey;

            // Make the request and get the response
            XmlDocument geocodeResponse = GetXmlResponse(requestUrl);

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
            string elevationUrl = spell_elevation_query_url(g_query_result);
            List<double> alt_list = get_elevations(elevationUrl);

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

        private void save_gpx_button_Click(object sender, RoutedEventArgs e)
        {
            var coords = locations.Select(l => new Coordinate(l.Latitude, l.Longitude)).ToList();

            var gpx = new Gpx11Serializer();

            var gpsData = new GpsData();

            var track = new Track();
            var segment = new TrackSegment();
            track.Segments.Add(segment);

            foreach (var loc in locations)
            {
                segment.Waypoints.Add(new Waypoint(loc.Latitude, loc.Longitude));
            }

            gpsData.Tracks.Add(track);
            string gpxData = gpx.Serialize(gpsData);

            SaveFileDialog gpx_save_dlg = new SaveFileDialog
            {
                InitialDirectory = @"D:\",
                Title = "Save GPX Files",

                CheckPathExists = true,

                DefaultExt = "GPX",
                Filter = "GPX files (*.gpx)|*.gpx",
                FilterIndex = 2,
                RestoreDirectory = true,

            };

            var result = gpx_save_dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                if (gpx_save_dlg.FileName != null)
                {
                    File.WriteAllText(gpx_save_dlg.FileName, gpxData);
                }
            }
            
        }
    }
}
