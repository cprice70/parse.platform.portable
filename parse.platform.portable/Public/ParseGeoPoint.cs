// Copyright (c) 2015-present, Parse, LLC.  All rights reserved.  This source code is licensed under the BSD-style license found in the LICENSE file in the root directory of this source tree.  An additional grant of patent rights can be found in the PATENTS file in the same directory.

using System;
using System.Collections.Generic;
using Parse.Internal.Utilities;

namespace Parse.Public
{
    /// <inheritdoc cref="IJsonConvertible" />
    ///  <summary>
    ///  ParseGeoPoint represents a latitude / longitude point that may be associated
    ///  with a key in a ParseObject or used as a reference point for geo queries.
    ///  This allows proximity-based queries on the key.
    ///  Only one key in a class may contain a GeoPoint.
    ///  </summary>
    public struct ParseGeoPoint : IJsonConvertible
    {
        /// <summary>
        /// Constructs a ParseGeoPoint with the specified latitude and longitude.
        /// </summary>
        /// <param name="latitude">The point's latitude.</param>
        /// <param name="longitude">The point's longitude.</param>
        public ParseGeoPoint(double latitude, double longitude)
            : this()
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        private double _latitude;

        /// <summary>
        /// Gets or sets the latitude of the GeoPoint. Valid range is [-90, 90].
        /// Extremes should not be used.
        /// </summary>
        public double Latitude
        {
            get => _latitude;
            set
            {
                if (value > 90 || value < -90)
                {
                    throw new ArgumentOutOfRangeException("value",
                        "Latitude must be within the range [-90, 90]");
                }

                _latitude = value;
            }
        }

        private double _longitude;

        /// <summary>
        /// Gets or sets the longitude. Valid range is [-180, 180].
        /// Extremes should not be used.
        /// </summary>
        public double Longitude
        {
            get => _longitude;
            set
            {
                if (value > 180 || value < -180)
                {
                    throw new ArgumentOutOfRangeException("value",
                        "Longitude must be within the range [-180, 180]");
                }

                _longitude = value;
            }
        }

        /// <summary>
        /// Get the distance in radians between this point and another GeoPoint. This is the smallest angular
        /// distance between the two points.
        /// </summary>
        /// <param name="point">GeoPoint describing the other point being measured against.</param>
        /// <returns>The distance in between the two points.</returns>
        public ParseGeoDistance DistanceTo(ParseGeoPoint point)
        {
            const double d2R = Math.PI / 180; // radian conversion factor
            var lat1Rad = Latitude * d2R;
            var long1Rad = _longitude * d2R;
            var lat2Rad = point.Latitude * d2R;
            var long2Rad = point.Longitude * d2R;
            var deltaLat = lat1Rad - lat2Rad;
            var deltaLong = long1Rad - long2Rad;
            var sinDeltaLatDiv2 = Math.Sin(deltaLat / 2);
            var sinDeltaLongDiv2 = Math.Sin(deltaLong / 2);
            // Square of half the straight line chord distance between both points.
            // [0.0, 1.0]
            var a = sinDeltaLatDiv2 * sinDeltaLatDiv2 +
                       Math.Cos(lat1Rad) * Math.Cos(lat2Rad) * sinDeltaLongDiv2 * sinDeltaLongDiv2;
            a = Math.Min(1.0, a);
            return new ParseGeoDistance(2 * Math.Asin(Math.Sqrt(a)));
        }

        IDictionary<string, object> IJsonConvertible.ToJson()
        {
            return new Dictionary<string, object>
            {
                {"__type", "GeoPoint"},
                {"latitude", Latitude},
                {"longitude", Longitude}
            };
        }
    }
}