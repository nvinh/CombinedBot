using CombinedBot.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace CombinedBot.Controllers
{
    public class CurrencyExchange
    {
        public static async Task<double?> GetCurrencyRateAsync(string CurrencySymbol)
        {
            try
            {
                //--- get rate USD<->NZD first. Cannot directly convert because this FREE API doesn't allow
                string ServiceURL = $"http://www.apilayer.net/api/live?access_key=485e09bfad0c5453444322b6e953b4d0&currencies=NZD&format=1&source=USD";
                //using (WebClient client = new WebClient())
                //{
                //    ResultInJSON = await client.DownloadStringTaskAsync(ServiceURL).ConfigureAwait(false);
                //}
                CurrencyObject.RootObject rootObject;

                //    Console.WriteLine(activity.Attachments[0].ContentUrl);

                HttpClient client = new HttpClient();
                string ResultInJSON = await client.GetStringAsync(new Uri(ServiceURL));
                //string ResultInJSON = await client.GetStringAsync(new Uri("http://api.openweathermap.org/data/2.5/weather?q=" + activity.Text + "&units=metric&APPID=440e3d0ee33a977c5e2fff6bc12448ee"));

                rootObject = JsonConvert.DeserializeObject<CurrencyObject.RootObject>(ResultInJSON);

                double USD2NZD = rootObject.quotes.USDNZD;

                //--- get rate USD<->CurrencySymbol second. Cannot directly convert because this FREE API doesn't allow
                ServiceURL = $"http://www.apilayer.net/api/live?access_key=485e09bfad0c5453444322b6e953b4d0&currencies=" + CurrencySymbol + "& format=1&source=USD";
                CurrencyObject.RootObject rootObject2;
                ResultInJSON = await client.GetStringAsync(new Uri(ServiceURL));
                rootObject2 = JsonConvert.DeserializeObject<CurrencyObject.RootObject>(ResultInJSON);

                //Type typ = typeof(CurrencyObject.RootObject);
                //var f = typ.GetField("quotes.USD" + CurrencySymbol.ToUpper());

                //double val = f.GetValue(rootObject2);
                double result = 1;
                double val = 1;
                if (rootObject2.success)
                {
                    if (Double.TryParse(rootObject2.quotes["USD" + CurrencySymbol.ToUpper()].ToString(), out val) && USD2NZD>0)
                    {
                        result = val / USD2NZD;
                    }
                    else
                    {
                        return null;
                    }
                } else
                {
                    return null;
                }
                //double USD2CurrencySymbol = val;                
                return result;
            }
            catch (WebException ex)
            {
                //handle your exception here  
                throw ex;
            }
        }
    }
}