using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CombinedBot.Models
{
    public static class Common
    {
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
        public static bool GetCarInfo(int carID, ref int carPrice, ref string carName)
        {
            CombinedBotDBEntities DB = new CombinedBotDBEntities();
            List<Vehicle> carList = DB.Vehicles.ToList();
            bool found = false;
            foreach (Vehicle car in carList)
            {
                if (car.Id == carID)
                {
                    carName = car.VehicleName;
                    carPrice = (car.Price == null) ? 0 : car.Price.Value;
                    found = true;
                }
            }
            return found;
        }
    }
}