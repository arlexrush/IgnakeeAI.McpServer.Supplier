namespace IgnakeeAI.McpServer.Labor.Domain.Utils
{
    /// <summary>Utilidades de cálculo geográfico compartidas entre capas.</summary>
    public static class GeoUtils
    {
        /// <summary>Calcula la distancia en km entre dos coordenadas usando la fórmula de Haversine.</summary>
        public static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;
            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                  + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static double ToRad(double deg) => deg * Math.PI / 180.0;
    }
}
