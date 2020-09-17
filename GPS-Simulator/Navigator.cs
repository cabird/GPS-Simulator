//
//  Created by Richard Zhang (Richard.Rupo.Zhang@gmail.com) on 3/2020
//  Copyright © 2020 Richard Zhang. All rights reserved.
//
// Bing MAP WPF reference
using Microsoft.Maps.MapControl.WPF;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GPS_Simulator
{
    public enum EndOfRouteBehavior 
    { 
        Stop, 
        Reverse, 
        Loop
    }
    public enum RoutingMode
    {
        OnRoute,
        TowardsPoint,
        Stopped
    }
    public enum WalkingState
    {
        Active,
        Paused,
        Stopped
    }

    class Navigator
    {
        public EndOfRouteBehavior EndOfRouteBehavior { get; private set; } = EndOfRouteBehavior.Stop;
        public RoutingMode RoutingMode { get; set; } = RoutingMode.Stopped;
        public WalkingState WalkingState { get; set; }

        public Location TargetPointLocation { get; set; }

        private LocationCollection route;
        private int curRouteIndex;

        public Location CurrentLocation { private set; get; }
        
        public double walkingSpeed {
            set;
            get;
        }

        public Navigator()
        {

        }

        public static double distance_on_loc(Location loc1, Location loc2)
        {
            return distance_on_geoid(loc1.Latitude,
                    loc1.Longitude,
                    loc2.Latitude,
                    loc2.Longitude);
        }

        public static double distance_on_geoid(double lat1, double lon1, double lat2, double lon2)
        {
            // Convert degrees to radians
            lat1 = lat1 * Math.PI / 180.0;
            lon1 = lon1 * Math.PI / 180.0;

            lat2 = lat2 * Math.PI / 180.0;
            lon2 = lon2 * Math.PI / 180.0;

            // radius of earth in metres
            double r = 6378100;

            // P
            double rho1 = r * Math.Cos(lat1);
            double z1 = r * Math.Sin(lat1);
            double x1 = rho1 * Math.Cos(lon1);
            double y1 = rho1 * Math.Sin(lon1);

            // Q
            double rho2 = r * Math.Cos(lat2);
            double z2 = r * Math.Sin(lat2);
            double x2 = rho2 * Math.Cos(lon2);
            double y2 = rho2 * Math.Sin(lon2);

            // Dot product
            double dot = (x1 * x2 + y1 * y2 + z1 * z2);
            double cos_theta = dot / (r * r);

            double theta = Math.Acos(cos_theta);

            // Distance in Metres
            return r * theta;
        }

        // radius of earth
        const double radius = 6378100;
        public static double Bearing(Location pt1, Location pt2)
        {
            double x = Math.Cos(DegreesToRadians(pt1.Latitude))
                * Math.Sin(DegreesToRadians(pt2.Latitude))
                - Math.Sin(DegreesToRadians(pt1.Latitude))
                * Math.Cos(DegreesToRadians(pt2.Latitude))
                * Math.Cos(DegreesToRadians(pt2.Longitude - pt1.Longitude));

            double y = Math.Sin(DegreesToRadians(pt2.Longitude - pt1.Longitude))
                * Math.Cos(DegreesToRadians(pt2.Latitude));

            // Math.Atan2 can return negative value, 0 <= output value < 2*PI expected 
            return (Math.Atan2(y, x) + Math.PI * 2) % (Math.PI * 2);
        }

        public static double DegreesToRadians(double angle)
        {
            return angle * Math.PI / 180.0d;
        }

        public static double RadiansToDegrees(double radians)
        {
            const double radToDegFactor = 180 / Math.PI;
            return radians * radToDegFactor;
        }

        public static Location FindPointAtDistanceFrom(Location startPoint,
            double bearing, double distance)
        {

            var distRatio = distance / radius;
            var distRatioSine = Math.Sin(distRatio);
            var distRatioCosine = Math.Cos(distRatio);

            var startLatRad = DegreesToRadians(startPoint.Latitude);
            var startLonRad = DegreesToRadians(startPoint.Longitude);

            var startLatCos = Math.Cos(startLatRad);
            var startLatSin = Math.Sin(startLatRad);

            var endLatRads = Math.Asin((startLatSin * distRatioCosine)
                + (startLatCos * distRatioSine * Math.Cos(bearing)));

            var endLonRads = startLonRad
                + Math.Atan2(
                    Math.Sin(bearing) * distRatioSine * startLatCos,
                    distRatioCosine - startLatSin * Math.Sin(endLatRads));

            return new Location
            {
                Latitude = RadiansToDegrees(endLatRads),
                Longitude = RadiansToDegrees(endLonRads)
            };
        }

        public static Location GetLocationBetweenTwoLocations(Location loc1, Location loc2)
        {
            Location midLoc = new Location((loc1.Latitude + loc2.Latitude) / 2, 
                (loc1.Longitude + loc2.Longitude) / 2, 
                (loc1.Altitude + loc2.Altitude) / 2);
            return midLoc;
        }

        /// <summary>
        /// calculate the next step.
        /// </summary>
        /// <returns></returns>
        /// 
        public Location GetNextStepLocation(bool doGpsDrift)
        {
            if (WalkingState == WalkingState.Paused || WalkingState == WalkingState.Stopped) return CurrentLocation;

            switch(RoutingMode)
            {
                case RoutingMode.OnRoute:
                    CurrentLocation = GetNextSteplocationOnRoute();
                    break;
                case RoutingMode.TowardsPoint:
                    CurrentLocation = GetNextStepTowardsPoint();
                    break;
                case RoutingMode.Stopped:
                    return CurrentLocation;
            }
            return doGpsDrift ? AddGpsDriftToLocation(CurrentLocation) : CurrentLocation ;
        }

        private Location GetNextStepTowardsPoint()
        {
            Location next_location = new Location();
            double dis_to_next_seg = distance_on_loc(CurrentLocation,
                TargetPointLocation);

            // check if the potential next step is out of 
            // the range of current segment.
            double dis_walk_500ms = walkingSpeed / 2;

            if (dis_walk_500ms < dis_to_next_seg)
            {
                // current segment.
                double bearing = Bearing(CurrentLocation, TargetPointLocation);

                next_location = FindPointAtDistanceFrom(CurrentLocation,
                    bearing,
                    dis_walk_500ms);
            }
            else
            {
                next_location = TargetPointLocation;
            }
            return next_location;
        }

        private Location GetNextSteplocationOnRoute()
        {
            Location next_location = new Location();
            double dis_to_next_seg = distance_on_loc(CurrentLocation,
                route[curRouteIndex + 1]);

            // check if the potential next step is out of 
            // the range of current segment.
            double dis_walk_500ms = walkingSpeed / 2;

            if (dis_walk_500ms < dis_to_next_seg)
            {
                // current segment.
                double bearing = Bearing(route[curRouteIndex],
                    route[curRouteIndex + 1]);

                bearing = Bearing(CurrentLocation, route[curRouteIndex + 1]);

                next_location = FindPointAtDistanceFrom(CurrentLocation,
                    bearing,
                    dis_walk_500ms);
            }
            else
            {
                // move to the next segment.
                curRouteIndex++;

                // the end of whole track.
                if (curRouteIndex >= route.Count - 1)
                {
                    if (EndOfRouteBehavior == EndOfRouteBehavior.Reverse)
                    {
                        // loop
                        curRouteIndex = 0;
                    } else if (EndOfRouteBehavior == EndOfRouteBehavior.Reverse)
                    { // reverse the walk AB --> BA -->AB.
                        route.Reverse(); 
                        return route[curRouteIndex];
                    } else
                    {
                        RoutingMode = RoutingMode.Stopped;
                        curRouteIndex = 0;
                        return CurrentLocation;
                    }
                } 

                double mode_dis = dis_walk_500ms - dis_to_next_seg;
                double bearing = Bearing(route[curRouteIndex],
                   route[curRouteIndex + 1]);

                next_location = FindPointAtDistanceFrom(
                    route[curRouteIndex],
                    bearing,
                    mode_dis);
            }

            return next_location;
        }

        public void TeleportToLocation(Location loc)
        {
            CurrentLocation = loc;
            RoutingMode = RoutingMode.Stopped;
        }

        /// <summary>
        /// the timer callback -- walk one step
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public static Location AddGpsDriftToLocation(Location location)
        {
            // drift the GPS (lon, lat)
            Random rnd = new Random();
            double lon_drift = rnd.NextDouble() * (0.00004 - 0.00001) + 0.00001;
            double lat_drift = rnd.NextDouble() * (0.00004 - 0.00001) + 0.00001;
            int direction = (rnd.Next(0, 1) > 0) ? 1 : -1;

            return new Location(
                location.Latitude + lat_drift * direction,
                location.Longitude + lon_drift * direction
                );
        }

        public void WalkToLocation(Location location)
        {
            TargetPointLocation = location;
            RoutingMode = RoutingMode.TowardsPoint;
            WalkingState = WalkingState.Active;
        }

        public void StartWalkingRoute()
        {
            curRouteIndex = 0;
            CurrentLocation = route[0];
            WalkingState = WalkingState.Active;
            RoutingMode = RoutingMode.OnRoute;
        }

        /// <summary>
        /// set route to walk
        /// </summary>
        /// <param name="polyline"></param>
        public void SetRoute(LocationCollection route)
        {
            this.route = route;
            curRouteIndex = 0;
        }
    }
}
