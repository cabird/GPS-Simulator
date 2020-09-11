using Geo;
using Geo.Gps;
using Geo.Gps.Serialization;
using Microsoft.Maps.MapControl.WPF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace GPS_Simulator
{
    public class GpxUtils
    {

        public static DialogResult OpenGpxDlg(out string gpxFileName)
        {
            gpxFileName = null;
            OpenFileDialog gpxOpenDlg = new OpenFileDialog
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

            var result = gpxOpenDlg.ShowDialog();
            if (result == DialogResult.OK && gpxOpenDlg.FileName != null)
            {
                gpxFileName = gpxOpenDlg.FileName;
                return result;
            }
            return DialogResult.Cancel;

        }

        public static LocationCollection ReadGpxCoords(string gpx_file_name)
        {
            XDocument gpx_file = XDocument.Load(gpx_file_name);
            XNamespace gpx = XNamespace.Get("http://www.topografix.com/GPX/1/1");

            var locations = new LocationCollection();

            var waypoints = from waypoint in gpx_file.Descendants(gpx + "wpt")
                            select new
                            {
                                Latitude = waypoint.Attribute("lat").Value,
                                Longitude = waypoint.Attribute("lon").Value,
                                Elevation = waypoint.Element(gpx + "ele") != null ? waypoint.Element(gpx + "ele").Value : null
                            };

            foreach (var wpt in waypoints)
            {
                locations.Add(new Location(Convert.ToDouble(wpt.Latitude),
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
                    locations.Add(new Location(Convert.ToDouble(trkSeg.Latitude),
                        Convert.ToDouble(trkSeg.Longitude),
                        Convert.ToDouble(trkSeg.Elevation)));
                }
            }

            return locations;
        }

        public static DialogResult SaveGpxDialog(out string gpxFileName)
        {
            gpxFileName = null;
            var gpxSaveDlg = new SaveFileDialog
            {
                InitialDirectory = @"D:\",
                Title = "Save GPX Files",

                CheckPathExists = true,

                DefaultExt = "GPX",
                Filter = "GPX files (*.gpx)|*.gpx",
                FilterIndex = 2,
                RestoreDirectory = true,

            };

            var result = gpxSaveDlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK && gpxSaveDlg.FileName != null)
            {
                gpxFileName = gpxSaveDlg.FileName;
                return result;

            }
            return DialogResult.Cancel;
            
        }

        public static void SaveGpxCoordinates(LocationCollection route, string gpxFileName)
        {
            var coords = route.Select(l => new Coordinate(l.Latitude, l.Longitude)).ToList();

            var gpx = new Gpx11Serializer();
            var gpsData = new GpsData();
            var track = new Track();
            var segment = new TrackSegment();
            track.Segments.Add(segment);

            foreach (var loc in route) segment.Waypoints.Add(new Waypoint(loc.Latitude, loc.Longitude));

            gpsData.Tracks.Add(track);
            string gpxData = gpx.Serialize(gpsData);
            File.WriteAllText(gpxFileName, gpxData);

        }

    }
}
