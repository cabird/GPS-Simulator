using Microsoft.Maps.MapControl.WPF;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GPS_Simulator
{
    class MapUIManager
    {

        private Map map;

        public Pushpin curLocationPin;
        private List<Pushpin> pins;
        private MapPolyline polyline;
        private MainWindow mainWindow;
        private LocationCollection route;
        private LocationCollection snappedRoute;

        public MapUIManager(Map map, MainWindow wnd)
        {
            this.map = map;
            mainWindow = wnd;
            pins = new List<Pushpin>();
            polyline = new MapPolyline();

            map.MouseDoubleClick += this.Map_MouseDoubleClick;
            map.MouseUp += this.map_MouseUp;
            map.MouseMove += this.map_MouseMove;
        }

        /*
         * private Location GetLocationForMousePosition(Point mousePosition)
        {
            var point = map.TransformToAncestor(mainWindow).Transform(new Point(0, 0));
            mousePosition.Offset(-point.X, -point.Y);
            Location location = map.ViewportPointToLocation(mousePosition);
            location.Altitude = mainWindow.webServices.GetElevationForLocation(location);
            return location;
        }
        */

        public void SetRoute(LocationCollection route, LocationCollection snappedRoute = null)
        {
            this.route = route;
            this.snappedRoute = snappedRoute;
            DrawWayPoints();
        }

        

        Color startColor = Colors.ForestGreen;
        Color endColor = Colors.DarkRed;
        Color routeColor = Colors.DarkBlue;

        public void DrawWayPoints()
        {
            ClearWayPoints();
            foreach (var loc in route)
            {
                var pin = new Pushpin();
                pin.Location = loc;
                pin.MouseDown += Pin_MouseDown;
                pin.MouseUp += Pin_MouseUp;
                pins.Add(pin);

                // draw the tack on map
                Color pinColor = loc == route.First() ? startColor : loc == route.Last() ? endColor : routeColor;
                pin.Background = new SolidColorBrush(pinColor);
                map.Children.Add(pin);
            }

            // draw the line of the route
            polyline = new MapPolyline();
            polyline.Locations = snappedRoute ?? route;
            polyline.Stroke = new System.Windows.Media.SolidColorBrush(routeColor);
            polyline.StrokeThickness = 3;
            polyline.Opacity = 0.7;

            map.Children.Add(polyline);
        }

        public void SetCurrentPushpinLocation(Location location)
        {
            if (curLocationPin == null)
            {
                curLocationPin = new Pushpin();
                map.Children.Add(curLocationPin);
            }
            curLocationPin.Location = location;
        }

        public void ClearWayPoints()
        {
            if (map.Children.Contains(polyline)) map.Children.Remove(polyline);
            
            foreach (var pin in pins)
            {
                map.Children.Remove(pin);
            }
            pins.Clear();
        }

        private Pushpin selectedPushpin;
        private bool inPushpinDrag = false;
        private Location oldPinLocation;
        private Vector mouseToMarker;

        private void Pin_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            // we only care about this event on a pushpin
            var pushpin = sender as Pushpin;
            if (pushpin == null) return;

            if (e.LeftButton == MouseButtonState.Pressed && e.RightButton == MouseButtonState.Released)
            {
                selectedPushpin = pushpin;
                inPushpinDrag = true;
                oldPinLocation = selectedPushpin.Location;
                mouseToMarker = Point.Subtract(
                  map.LocationToViewportPoint(selectedPushpin.Location),
                  e.GetPosition(map));
            }
        }

        private void Pin_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                ContextMenu cm = mainWindow.FindResource("cmPushpin") as ContextMenu;
                cm.PlacementTarget = sender as Pushpin;
                cm.DataContext = sender as Pushpin;
                cm.IsOpen = true;
                e.Handled = true;
            }
        }

        private void map_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed &&
                inPushpinDrag && selectedPushpin != null)
            {
                selectedPushpin.Location = map.ViewportPointToLocation(
                    Point.Add(e.GetPosition(map), mouseToMarker));
                e.Handled = true;
            }
        }

        private void map_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (inPushpinDrag && selectedPushpin != null)
            {
                inPushpinDrag = false;
                int oldPinIndex = route.IndexOf(oldPinLocation);
                route[oldPinIndex] = selectedPushpin.Location;
                DrawWayPoints();
            }

            if (e.ChangedButton == MouseButton.Right)
            {
                ContextMenu cm = mainWindow.FindResource("cmMap") as ContextMenu;
                Location loc = map.ViewportPointToLocation(e.GetPosition(map));
                cm.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                cm.DataContext = loc;
                cm.IsOpen = true;
                e.Handled = true;
            }
        }

        /// <summary>
        /// double click and teleport.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Map_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var loc = map.ViewportPointToLocation(e.GetPosition(map));
            route.Add(loc);
            DrawWayPoints();
            e.Handled = true;
            return;
        }

        private void AddLocationBetweenLocationAndNext(int locIndex)
        {
            Debug.Assert(locIndex < route.Count);
            Location midPoint = Navigator.GetLocationBetweenTwoLocations(route[locIndex], route[locIndex + 1]);
            route.Insert(locIndex + 1, midPoint);
        }

        public void AddPinBeforeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"Add pin before");
            var control = sender as Control;
            var pin = control.DataContext as Pushpin;

            int locIndex = route.IndexOf(pin.Location);

            if (locIndex > 0)
            {
                AddLocationBetweenLocationAndNext(locIndex - 1);
                DrawWayPoints();
            }
        }

        public void AddPinAfterMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"Add pin after");
            var control = sender as Control;
            var pin = control.DataContext as Pushpin;

            int locIndex = route.IndexOf(pin.Location);

            if (locIndex < route.Count - 1)
            {
                AddLocationBetweenLocationAndNext(locIndex);
                DrawWayPoints();
            }
        }

        public void RemovePinMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"Removing pin");
            var control = sender as Control;
            var pin = control.DataContext as Pushpin;

            //remove the pushpin location from the route and redraw the route.
            int locIndex = route.IndexOf(pin.Location);
            if (locIndex < 0)
            {
                throw new Exception("can't find the location index for the removed pin location");
            }
            route.RemoveAt(locIndex);

            
            DrawWayPoints();
        }

    }
}
