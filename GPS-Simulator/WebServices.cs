using Microsoft.Maps.MapControl.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;

namespace GPS_Simulator
{
    public class WebServices
    {
        private string BingMapKey { get; set; }
        public WebServices(string bingMapKey)
        {
            BingMapKey = bingMapKey;
        }

        private static XmlDocument GetXmlResponse(string requestUrl)
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

        public XmlDocument DoBingLocationSearch(string query)
        {
            string requestUrl = @"http://dev.virtualearth.net/REST/v1/Locations/" + query + "?o=xml&key=" + BingMapKey;

            // Make the request and get the response
            XmlDocument geocodeResponse = WebServices.GetXmlResponse(requestUrl);
            return geocodeResponse;
        }

        public void GetDirectionsWaypointList(Location location1, Location location2, out List<Location> routeWaypoints, out List<Location> snappedRoute)
        {
            var loc1Str = $"{location1.Latitude},{location1.Longitude}";
            var loc2Str = $"{location2.Latitude},{location2.Longitude}";
            GetDirectionsWaypointList(loc1Str, loc2Str, out routeWaypoints, out snappedRoute);
        }

        public void GetDirectionsWaypointList(string location1, string location2, out List<Location> routeWaypoints, out List<Location> snappedRoute)
        {
            var location1Encoded = HttpUtility.UrlEncode(location1);
            var location2Encoded = HttpUtility.UrlEncode(location2);
            string url = $"http://dev.virtualearth.net/REST/V1/Routes/Walking?wp.0={location1Encoded}&wp.1={location2Encoded}&optmz=distance&output=xml&key={BingMapKey}";

            var responseXML = GetXmlResponse(url);

            XNamespace restNS = "http://schemas.microsoft.com/search/local/ws/rest/v1";
            XDocument xDoc;
            using (var nodeReader = new XmlNodeReader(responseXML))
            {
                nodeReader.MoveToContent();
                xDoc = XDocument.Load(nodeReader);
            }

            routeWaypoints = xDoc.Descendants(restNS + "Route")?.First()
                .Descendants(restNS + "ItineraryItem")
                .Select(node => new Location(
                    double.Parse(node.Descendants(restNS + "Latitude").First().Value.Trim()),
                    double.Parse(node.Descendants(restNS + "Longitude").First().Value.Trim()))
                ).ToList();

            snappedRoute = SnapDirectionsToRoads(routeWaypoints);
        }

        public List<Location> SnapDirectionsToRoads(List<Location> waypoints)
        {
            var travelMode = "walking";
            var interpolate = "true";
            var points = string.Join(";", waypoints.Select(loc => loc.Latitude + "," + loc.Longitude));
            string url = $"http://dev.virtualearth.net/REST/v1/Routes/SnapToRoad?points={points}&interpolate={interpolate}&travelMode={travelMode}&o=xml&key={BingMapKey}";

            var responseXML = GetXmlResponse(url);

            XNamespace restNS = "http://schemas.microsoft.com/search/local/ws/rest/v1";
            XDocument xDoc;
            using (var nodeReader = new XmlNodeReader(responseXML))
            {
                nodeReader.MoveToContent();
                xDoc = XDocument.Load(nodeReader);
            }

            var locList = xDoc.Descendants(restNS + "SnappedPoint")
                .Select(node => new Location(
                    double.Parse(node.Descendants(restNS + "Latitude").First().Value.Trim()),
                    double.Parse(node.Descendants(restNS + "Longitude").First().Value.Trim()))
                ).ToList();

            return locList;
        }

        public double GetElevationForLocation(Location location)
        {
            string url = SpellElevationQueryUrl(location);
            return GetElevations(url).First();
        }

        public List<double> GetElevationsForLocations(IEnumerable<Location> locations)
        {
            string url = SpellElevationQueryUrl(locations);
            return GetElevations(url);
        }

        // Format the URI from a list of locations.
        private string SpellElevationQueryUrl(IEnumerable<Location> locList)
        {
            // The base URI string. Fill in: 
            // {0}: The lat/lon list, comma separated. 
            // {1}: The key. 
            const string BASE_URI_STRING =
              "http://dev.virtualearth.net/REST/v1/Elevation/List?points={0}&key={1}&o=xml";

            string retVal = string.Empty;
            string locString = string.Join(",", locList.Select(loc => loc.Latitude.ToString() + "," + loc.Longitude.ToString()));

            retVal = string.Format(BASE_URI_STRING, locString, BingMapKey);
            return retVal;
        }

        // spell the url for single point.
        private string SpellElevationQueryUrl(Location loc)
        {
            // The base URI string. Fill in: 
            // {0}: The lat/lon list, comma separated. 
            // {1}: The key. 
            const string BASE_URI_STRING =
              "http://dev.virtualearth.net/REST/v1/Elevation/List?points={0}&key={1}&o=xml";

            string locString = loc.Latitude.ToString() + "," + loc.Longitude.ToString();
            return string.Format(BASE_URI_STRING, locString, BingMapKey);

        }

        private List<double> GetElevations(string url)
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


    }
}
