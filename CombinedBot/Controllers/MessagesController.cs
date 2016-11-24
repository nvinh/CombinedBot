using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using WeatherBot.Models;
using System.Collections.Generic;
using CombinedBot.Models;
using CombinedBot.Controllers;

namespace Combined_Bot
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        /// 
        private class Luis
        {
            public string intent { get; set; }
            public string entity { get; set; }
        }
        private class CarInfo
        {
            public string carName { get; set; }
            public int carPrice { get; set; }
        }
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            //------------- added 22 Nov 2016
            #region Set CurrentBaseURL and ChannelAccount
            // Get the base URL that this service is running at
            // This is used to show images
            string currentBaseURL =
                    this.Url.Request.RequestUri.AbsoluteUri.Replace(@"api/messages", "");

            // Create an instance of BotData to store data
            BotData objBotData = new BotData();

            // Instantiate a StateClient to save BotData            
            StateClient stateClient = activity.GetStateClient();

            // Use stateClient to get current userData
            BotData userData = await stateClient.BotState.GetUserDataAsync(
                activity.ChannelId, activity.From.Id);

            // Update userData by setting CurrentBaseURL and Recipient
            //userData.SetProperty<string>("CurrentBaseURL", currentBaseURL);

            // Save changes to userData
            //await stateClient.BotState.SetUserDataAsync(activity.ChannelId, activity.From.Id, userData);
            #endregion
            //------------- end of added 22 Nov
            if (activity.Type == ActivityTypes.Message)
            {
                ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));

                var userMessage = activity.Text;

                //StateClient stateClient = activity.GetStateClient();
                //BotData userData = await stateClient.BotState.GetUserDataAsync(activity.ChannelId, activity.From.Id);
                // *************************
                // Log to Database
                // *************************
                // Instantiate the BotData dbContext
                CombinedBotDBEntities DB = new CombinedBotDBEntities();
                // Create a new MessageLog object
                MessageLog NewMsgLog = new MessageLog();
                // Set the properties on the MessageLog object
                NewMsgLog.Channel = activity.ChannelId;
                NewMsgLog.UserID = activity.From.Id;
                NewMsgLog.UserName = activity.From.Name;
                NewMsgLog.Created = DateTime.UtcNow;
                NewMsgLog.Message = activity.Text.Truncate(500);
                // Add the MessageLog object to MessageLogs
                DB.MessageLogs.Add(NewMsgLog);
                // Save the changes to the database
                DB.SaveChanges();
                //----------------
                string endOutput = "Hi";
                bool textAnswer = true;
                bool needtoSaveUserData = false;
                bool sentGreeting = userData.GetProperty<bool>("SentGreeting");
                int capacity = userData.GetProperty<int>("Capacity");
                string hiringDay = userData.GetProperty<string>("HiringDay");
                string customerAdd = userData.GetProperty<string>("CustomerAdd");
                int selectedCarId = userData.GetProperty<int>("SelectedCarId");
                bool resetFlag = false;
                //-------- local variables
                int carPrice = 0;
                string carName = "";
                //----------------
                if (userMessage.ToLower().Contains("reset"))
                {
                    sentGreeting = false;
                    capacity = 0;
                    hiringDay = "";
                    customerAdd = "";
                    selectedCarId = 0;
                    //userData.SetProperty<bool>("SentGreeting", sentGreeting);
                    userData.SetProperty<int>("Capacity", capacity);
                    userData.SetProperty<string>("HiringDay", hiringDay);
                    userData.SetProperty<string>("CustomerAdd", customerAdd);
                    userData.SetProperty<int>("SelectedCarId", selectedCarId);
                    //endOutput = "The conversation has just been restarted from beggining.";
                    needtoSaveUserData = true;
                    resetFlag = true;
                }
                //sentGreeting = false;
                //resetFlag = true;   // for debugging on server
                if (!resetFlag)
                {
                    //---- try to detect several basic input commands first
                    if (capacity <= 0)
                    {
                        bool result = Int32.TryParse(userMessage, out capacity);
                        if (result)
                        {
                            userData.SetProperty<int>("Capacity", capacity);
                            needtoSaveUserData = true;
                        }
                    } else if (capacity > 0 && selectedCarId <= 0)
                    {
                        bool result = Int32.TryParse(userMessage, out selectedCarId);
                        if (result)
                        {
                            userData.SetProperty<int>("SelectedCarId", selectedCarId);
                            needtoSaveUserData = true;
                        }
                    }
                }
                if (!sentGreeting)
                {
                    sentGreeting = true;
                    endOutput = "Hello. What can I help you with?";
                    userData.SetProperty<bool>("SentGreeting", sentGreeting);
                    needtoSaveUserData = true;
                    //await stateClient.BotState.SetUserDataAsync(activity.ChannelId, activity.From.Id, userData);
                }
                else //if (1==2)
                {
                    if (userMessage.ToLower().Contains("hire"))
                    {
                        endOutput = "Great! Thanks for choosing our service.";
                    }
                    if (capacity <= 0)
                    {
                        endOutput += " You need a car for how many people?";
                    }
                    if (capacity > 0 && selectedCarId <= 0)
                    {
                        endOutput = "You need " + capacity.ToString() + " seats. We have above vehicles that match your requirements, please select one.";
                        //list cars here
                        HttpResponseMessage x = await ListAllCars(capacity, currentBaseURL, activity, connector);
                        //bool x = await ListAllCars(capacity, currentBaseURL, activity, connector);
                    }
                    if (selectedCarId > 0)
                    {
                        bool carFound = Common.GetCarInfo(selectedCarId, ref carPrice, ref carName);
                        if (carFound)
                        {
                            endOutput = "You selected the car " + carName + " (ID=" + selectedCarId.ToString() + "). A very smart choice! The hiring fee is $" + carPrice + "NZD per day";
                        }
                        else
                        {
                            endOutput = "Your selected car cannot be found. Please type reset to restart again.";
                        }
                    }
                    Luis question = await GetEntityFromLUIS(userMessage);
                    //endOutput = question.intent;
                    if (question.intent == "currencyconvert" && carPrice > 0)
                    {
                        string convCarPrice = await GetExchange(question.entity, carPrice);
                        endOutput = convCarPrice;
                    }
                    if (question.intent == "carbranch" && (customerAdd==null || customerAdd=="")) //&& question.intent != "customeradd")
                    {
                        endOutput = "We have two branches in the city. Where are you living now?";
                    }
                    if (question.intent == "customeradd" && (customerAdd == null || customerAdd == ""))
                    {
                        customerAdd = userMessage;
                        // send recommendation about branch address
                        endOutput = await BranchRecommendation(customerAdd);
                    }
                }
                if (textAnswer)
                {
                    if (needtoSaveUserData) { await stateClient.BotState.SetUserDataAsync(activity.ChannelId, activity.From.Id, userData); }
                    // *************************
                    // Log to Database
                    // *************************
                    //endOutput= endOutput+ "-" + activity.ChannelId.ToString() + "-" + activity.From.Id + "-" + activity.From.Name;
                    // return our reply to the user
                    Activity infoReply = activity.CreateReply(endOutput);
                    // Set the properties on the MessageLog object
                    //NewMsgLog.Channel = infoReply.ChannelId;  // This part doesn't work, need to be checked
                    //NewMsgLog.UserID = infoReply.From.Id;
                    //NewMsgLog.UserName = infoReply.From.Name;
                    NewMsgLog.Channel = activity.ChannelId;
                    NewMsgLog.UserID = activity.From.Id;
                    NewMsgLog.UserName = activity.From.Name;
                    NewMsgLog.Created = DateTime.UtcNow;
                    NewMsgLog.Message = infoReply.Text.Truncate(500);
                    // Add the MessageLog object to MessageLogs
                    DB.MessageLogs.Add(NewMsgLog);
                    // Save the changes to the database
                    DB.SaveChanges();
                    await connector.Conversations.ReplyToActivityAsync(infoReply);
                }
            }
            else
            {
                HandleSystemMessage(activity);
            }
            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;
        }

        private Activity HandleSystemMessage(Activity message)
        {
            if (message.Type == ActivityTypes.DeleteUserData)
            {
                // Implement user deletion here
                // If we handle user deletion, return a real message
            }
            else if (message.Type == ActivityTypes.ConversationUpdate)
            {
                // Handle conversation state changes, like members being added and removed
                // Use Activity.MembersAdded and Activity.MembersRemoved and Activity.Action for info
                // Not available in all channels
            }
            else if (message.Type == ActivityTypes.ContactRelationUpdate)
            {
                // Handle add/remove from contact lists
                // Activity.From + Activity.Action represent what happened
            }
            else if (message.Type == ActivityTypes.Typing)
            {
                // Handle knowing tha the user is typing
            }
            else if (message.Type == ActivityTypes.Ping)
            {
            }

            return null;
        }
        private async Task<string> GetExchange(string currencySymbol, int carPrice)
        {
            double? dblExRateValue = await CurrencyExchange.GetCurrencyRateAsync(currencySymbol);
            if (dblExRateValue == null)
            {
                return string.Format("Your currency symbol cannot be found. Please give us a valid one, e.g USD, CAD, JPY, etc."); // \"{0}\" CurrencySymbol
            }
            else
            {
                dblExRateValue *= carPrice;
                return string.Format("We estimated that you need to pay ${0:0.##}{1} per day. Please contact a bank for more information.", dblExRateValue, currencySymbol.ToUpper());
            }
        }
        //--------------------
        private static async Task<Luis> GetEntityFromLUIS(string query)
        {
            string entity = "nzd";
            query = Uri.EscapeDataString(query);
            CurrencyBotLUIS Data = new CurrencyBotLUIS();
            CurrencyBotLUIS.RootObject rootObject= new CurrencyBotLUIS.RootObject();
            using (HttpClient client = new HttpClient())
            {
                //string RequestURI = "https://api.projectoxford.ai/luis/v1/application?id=7f626790-38d6-4143-9d46-fe85c56a9016&subscription-key=09f80de609fa4698ab4fe5249321d165&q=" + query;
                string RequestURI = "https://api.projectoxford.ai/luis/v2.0/apps/aacc4141-2867-46e4-889f-092bd124c14e?subscription-key=a3e835abd182441b93a1f3c40b8309aa&verbose=true&q=" + query;
                HttpResponseMessage msg = await client.GetAsync(RequestURI);

                if (msg.IsSuccessStatusCode)
                {
                    var JsonDataResponse = await msg.Content.ReadAsStringAsync();
                    Data = JsonConvert.DeserializeObject<CurrencyBotLUIS>(JsonDataResponse);
                    rootObject= JsonConvert.DeserializeObject<CurrencyBotLUIS.RootObject>(JsonDataResponse);
                }
            }
            //rootObject = JsonConvert.DeserializeObject<WeatherObject.RootObject>(x); topScoringIntent
            string intent = rootObject.topScoringIntent.intent;
            double score= rootObject.topScoringIntent.score;
            bool found = false;
            if (intent.ToLower().Contains("currencyconvert") && score>= 0.6)
            {
                intent = "currencyconvert";
                if (rootObject.entities.Count>0)
                {
                    entity = rootObject.entities[0].entity;
                } else
                {
                    entity = "error";
                }
                found = true;
            }
            if (intent.ToLower().Contains("carbranch") && score >= 0.6)
            {
                intent = "carbranch";
                found = true;
            }
            if (intent.ToLower().Contains("customeradd") && score >= 0.6)
            {
                intent = "customeradd";
                found = true;
            }
            if (!found) { intent = ""; }
            Luis result= new Luis();
            result.intent = intent;
            result.entity = entity;
            return result;
        }
        //----------------
        private async Task<HttpResponseMessage> ListAllCars(int capacity, string currentBaseURL, Activity activity, ConnectorClient connector)
        //private async Task<bool> ListAllCars(int capacity, string currentBaseURL, Activity activity, ConnectorClient connector)
        {
            // Instantiate the BotData dbContext
            Activity replyToConversation = activity.CreateReply("Vincent Cars");
            replyToConversation.Recipient = activity.From;
            replyToConversation.Type = "message";
            replyToConversation.Attachments = new List<Attachment>();
            if (1==1)
            {
                CombinedBotDBEntities DB = new CombinedBotDBEntities();
                List<Vehicle> carList = DB.Vehicles.ToList();
                foreach (Vehicle car in carList)
                {
                    if (car.Capacity >= capacity)
                    {
                        List<CardImage> cardImages = new List<CardImage>();
                        string strCarImg = String.Format(@"{0}/{1}", currentBaseURL, car.Image);
                        cardImages.Add(new CardImage(url: strCarImg));
                        List<CardAction> cardButtons = new List<CardAction>();
                        CardAction plButton = new CardAction()
                        {
                            //Value = "http://msa.ms",
                            //Type = "openUrl",
                            //Title = "MSA Website"
                            Type = "imBack",
                            Title = car.VehicleName.ToString(),
                            Value = car.Id.ToString()
                        };
                        cardButtons.Add(plButton);
                        //List<CardAction> cardButtons = CreateButtons();
                        // Create the Hero Card
                        // Set the image and the buttons
                        //ThumbnailCard plCard = new ThumbnailCard()
                        HeroCard plCard = new HeroCard()
                        {
                            Title = car.VehicleName,
                            Subtitle = car.Description + ", " + car.Capacity.ToString() + " seats, $" + car.Price.ToString() + "NZD/one day",
                            Images = cardImages,
                            Buttons = cardButtons
                        };
                        Attachment plAttachment = plCard.ToAttachment();
                        replyToConversation.Attachments.Add(plAttachment);
                    }
                }
            }            
            await connector.Conversations.SendToConversationAsync(replyToConversation);
            //Request.CreateResponse(HttpStatusCode.OK);
            return Request.CreateResponse(HttpStatusCode.OK);
            //await connector.Conversations.ReplyToActivityAsync(replyToConversation);
            //return true;
        }
        //----------------
        private static async Task<string> BranchRecommendation(string customerAdd)
        {
            string result = "";
            GoogleMap.RootObject rootObject;
            string branch1 = "955 New North Road, Auckland, New Zealand";
            string branch2 = "220 Queen Street, Auckland, New Zealand";
            HttpClient client = new HttpClient();
            string x = await client.GetStringAsync(new Uri("https://maps.googleapis.com/maps/api/directions/json?origin=" + customerAdd + "&destination=" + branch1 + "&key=AIzaSyAHq_8sdDdSm84d9QpXilwcu4uWrx7GWkU"));
            rootObject = JsonConvert.DeserializeObject<GoogleMap.RootObject>(x);
            int distance1 = rootObject.routes[0].legs[0].distance.value;

            x = await client.GetStringAsync(new Uri("https://maps.googleapis.com/maps/api/directions/json?origin=" + customerAdd + "&destination=" + branch2 + "&key=AIzaSyAHq_8sdDdSm84d9QpXilwcu4uWrx7GWkU"));
            rootObject = JsonConvert.DeserializeObject<GoogleMap.RootObject>(x);
            int distance2 = rootObject.routes[0].legs[0].distance.value;
            string branch = (distance1 < distance2) ? branch1 : branch2;
            // return our reply to the user
            //Activity replyToConversation = activity.CreateReply($"The distance from your address to our address is {distance} meters");
            result = "From your address, we think you should better to go to Vincent branch at " + branch + " (" + distance1.ToString() + "," + distance2.ToString() + "). ";
            result += "That branch is nearer from your address, you will save some money on the fuel.";
            return result;
        }
        //----------------
    }
}