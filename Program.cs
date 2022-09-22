using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using ISBM20ClientAdapter;
using ISBM20ClientAdapter.ResponseType;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace WorkManagementSystem
{
    class Program
    {
        static string _hostName = "";
        static string _channelId = "";
        static string _subscribeTopic = "";
        static string _publishTopic = "";
        static string _SubscribeSessionId = "";
        static string _PublishSessionId = "";

        static Boolean _authentication;
        static string _username = "";
        static string _password = "";


        static Boolean _isAutomaticallyApproved;
        static string _Description = "";
        static string _Asset = "";
        static string _WorkManagementType = "";
        static string _PriorityLevel = "";
        static string _PriorityScale = "";
        static string _WorkTaskType = "";
        static string _timeOfRequest = "";

        static ProviderPublicationService _myProviderPublicationService = new ProviderPublicationService();

        static ConsumerPublicationServices _myConsumerPublicationService = new ConsumerPublicationServices();

        static void Main(string[] args)
        {
            OpenSessions();

            while (true)
            {
                GetRequestforWork();

                //Determine work priority
                // High :  80-100, Normal: 50-79; Low: 0-49
                string Priority = "";
                int priorityScale = Int32.Parse(_PriorityScale);
                if ((priorityScale >= 0) && (priorityScale < 50))
                {
                    Priority = "Low";
                }
                else if ((priorityScale >= 50) && (priorityScale < 80))
                {
                    Priority = "Normal";
                }
                else if (priorityScale >= 80)
                {
                    Priority = "High";
                }

                if ((Priority == "High") && (_isAutomaticallyApproved == true))
                {
                    //Does not require approval:  Emergency Request
                    string EmergencyRequest = "SyncEmergencyWorkOrder.json";
                    string WorkDescription = System.IO.File.ReadAllText(EmergencyRequest);
                    PostWorkOrder(WorkDescription);
                }
                else if ((Priority == "High") && (_isAutomaticallyApproved != true))
                {
                    //Requires approval:  Emergency Request
                    string EmergencyRequest = "SyncEmergencyWorkOrder.json";
                    string WorkDescription = System.IO.File.ReadAllText(EmergencyRequest);
                    Console.WriteLine("Please review and approve this request for work >> y/n: ");
                    string Action = Console.ReadLine();
                    if (Action == "y")
                    {
                        PostWorkOrder(WorkDescription);
                    }
                    else if ((Action == "n"))
                    {
                        Console.WriteLine("Work request has been declined!!");
                        //break;
                    }
                    else
                    {
                        Console.WriteLine("Wrong entry!!");
                        //break;
                    }
                }
                else if ((_isAutomaticallyApproved == true) && ((Priority == "Normal") || (Priority == "Low")))
                {
                    //Does not require approval:  Maintenance Request  
                    string MaintenanceRequest = "SyncMaintenanceWorkOrder.json";
                    string WorkDescription = System.IO.File.ReadAllText(MaintenanceRequest);
                    PostWorkOrder(WorkDescription);
                }
                else if ((_isAutomaticallyApproved != true) && ((Priority == "Normal") || (Priority == "Low")))
                {
                    //Requires approval:  Maintenance Request  
                    string MaintenanceRequest = "SyncMaintenanceWorkOrder.json";
                    string WorkDescription = System.IO.File.ReadAllText(MaintenanceRequest);
                    Console.WriteLine("Please review and approve this request for work >> y/n: ");
                    string Action = Console.ReadLine();
                    if (Action == "y")
                    {
                        PostWorkOrder(WorkDescription);
                    }
                    else if ((Action == "n"))
                    {
                        Console.WriteLine("Work request has been declined!!\n");
                        //break;
                    }
                    else
                    {
                        Console.WriteLine("Wrong entry!!\n");
                        //break;
                    }
                }
            }

        }

        private static void OpenSessions()
        {
            //Read application configurations from Configs.json 
            string filename = "Configs_subscribe.json";
            SetConfigurations(filename);

            //Open a Consumer Publication Session
            // Make sure subscribe topic is different from publish topic
            //Also, it MUST be different from the decision systems publish and subscribe topics
            //This is to avoid interference and errors
            OpenSubscriptionSessionResponse myOpenSubscriptionSessionResponse = _myConsumerPublicationService.OpenSubscriptionSession(_hostName, _channelId, _subscribeTopic);
            Console.WriteLine("Host Address " + _hostName);
            Console.WriteLine("Channel Id " + _channelId);
            if (myOpenSubscriptionSessionResponse.StatusCode == 201)
            {
                //SessionID is stored in a class level valuable for repeatedly used in every BPD subscription operation.
                _SubscribeSessionId = myOpenSubscriptionSessionResponse.SessionID;
                Console.WriteLine("Subcription Session " + _SubscribeSessionId + "\n");
            }
            else
            {
                Console.WriteLine(myOpenSubscriptionSessionResponse.StatusCode + " " + myOpenSubscriptionSessionResponse.ISBMHTTPResponse);
                Console.WriteLine("Please check configurations!!");
            }

            Thread.Sleep(1000);

            filename = "Configs_publish.json";
            SetConfigurations(filename);
            //Open a Provider Publication Session
            OpenPublicationSessionResponse myOpenPublicationSessionResponse = _myProviderPublicationService.OpenPublicationSession(_hostName, _channelId);
            Console.WriteLine("\nHost Address " + _hostName);
            Console.WriteLine("Channel Id " + _channelId);
            if (myOpenPublicationSessionResponse.StatusCode == 201)
            {
                //SessionID is stored in a class level valuable for repeatedly used in every BPD post publication.
                _PublishSessionId = myOpenPublicationSessionResponse.SessionID;
                Console.WriteLine("Publication Session " + _PublishSessionId);
                Console.WriteLine("Decision Support System is running!!\n");
            }
            else
            {
                Console.WriteLine(myOpenPublicationSessionResponse.StatusCode + " " + myOpenPublicationSessionResponse.ISBMHTTPResponse);
                Console.WriteLine("Please check configurations!!");
            }

            Thread.Sleep(1000);

        }
        private static void SetConfigurations(string filename)
        {
            string JsonFromFile = System.IO.File.ReadAllText(filename);

            JObject JObjectConfigs = JObject.Parse(JsonFromFile);
            _hostName = JObjectConfigs["hostName"].ToString();
            _channelId = JObjectConfigs["channelId"].ToString();
            if (filename == "Configs_publish.json")
            {
                _publishTopic = JObjectConfigs["topic"].ToString();
            }
            if (filename == "Configs_subscribe.json")
            {
                _subscribeTopic = JObjectConfigs["topic"].ToString();
            }

            _authentication = (Boolean)JObjectConfigs["authentication"];
            if (_authentication == true)
            {
                _username = JObjectConfigs["userName"].ToString();
                _password = JObjectConfigs["password"].ToString();
            }
        }
        private static void GetRequestforWork()
        {
            //Read a Publication 
            ReadPublicationResponse myReadPublicationResponse = _myConsumerPublicationService.ReadPublication(_hostName, _SubscribeSessionId);

            // check if there is any work request in the queue
            if (myReadPublicationResponse.StatusCode == 200)
            {
                //Read parameter from syncAdvisory.json 
                JObject JObjectConfigs = JObject.Parse(myReadPublicationResponse.MessageContent);
                _Description = JObjectConfigs["processRequestsForWork"]["dataArea"]["requestsForWork"][0]["requestForWork"]["shortName"].ToString();
                _Asset = JObjectConfigs["processRequestsForWork"]["dataArea"]["requestsForWork"][0]["requestForWork"]["asset"]["shortName"].ToString();
                _WorkManagementType = JObjectConfigs["processRequestsForWork"]["dataArea"]["requestsForWork"][0]["requestForWork"]["workManagementType"]["shortName"].ToString();
                _WorkTaskType = JObjectConfigs["processRequestsForWork"]["dataArea"]["requestsForWork"][0]["requestForWork"]["workManagementType"]["shortName"].ToString();
                _timeOfRequest = JObjectConfigs["processRequestsForWork"]["applicationArea"]["creationDateTime"].ToString();
                _PriorityLevel = JObjectConfigs["processRequestsForWork"]["dataArea"]["requestsForWork"][0]["requestForWork"]["priorityLevelType"]["shortName"].ToString();
                _PriorityScale = JObjectConfigs["processRequestsForWork"]["dataArea"]["requestsForWork"][0]["requestForWork"]["priorityLevelType"]["priorityScale"]["numeric"].ToString();
                _isAutomaticallyApproved = (Boolean)JObjectConfigs["processRequestsForWork"]["dataArea"]["requestsForWork"][0]["requestForWork"]["isAutomaticallyApproved"];


                Console.WriteLine("\nSummary info: \n*****************************************************");
                Console.WriteLine("Work description: " + _Description);
                Console.WriteLine("Asset under risk: " + _Asset);
                Console.WriteLine("WorkManagementType: " + _WorkManagementType);
                Console.WriteLine("WorkTaskType: " + _WorkTaskType);
                Console.WriteLine("Priority Level: " + _PriorityLevel);
                Console.WriteLine("_isAutomaticallyApproved: " + _isAutomaticallyApproved);
                Console.WriteLine("Work Request Time: " + _timeOfRequest);
                Console.WriteLine("\n*****************************************************");

                //Remove publication from queue
                RemovePublicationResponse myRemovePublicationResponse = _myConsumerPublicationService.RemovePublication(_hostName, _SubscribeSessionId);

                //Acknowledge request for work
                string filename = "AcknowledgeRequestsForWork.json";
                string JsonFromFile = System.IO.File.ReadAllText(filename);
                string Topic = "OIIE:S30:V1.1/CCOM-JSON:SyncWorkAcknowledgement:V1.0";
                string confirmationBOD = JsonFromFile;
                PostPublicationResponse myPostPublicationResponse = _myProviderPublicationService.PostPublication(_hostName, _PublishSessionId, Topic, confirmationBOD);
                string MessageId = "";
                if (myPostPublicationResponse.StatusCode == 201)
                {
                    MessageId = myPostPublicationResponse.MessageID;
                    Console.WriteLine("\nWork request acknowledgement " + MessageId + " has been pusblished!!\n");
                }
                else
                {
                    Console.WriteLine(myPostPublicationResponse.StatusCode + " " + myPostPublicationResponse.ISBMHTTPResponse);
                }
            }
            else
            {
                Console.WriteLine(myReadPublicationResponse.ISBMHTTPResponse);

                Thread.Sleep(3000); // Sleep for 3 seconds

                GetRequestforWork();
            }

        }
        private static void PostWorkOrder(string bodMessage)
        {
            //Generate a Work Order. Whenever a work order is generated
            //using a particular topic, the responsible officer's system
            //should be subscribed to this topic and receive notifications.
            PostPublicationResponse myPostPublicationResponse = _myProviderPublicationService.PostPublication(_hostName, _PublishSessionId, _publishTopic, bodMessage);

            string MessageId = "";
            if (myPostPublicationResponse.StatusCode == 201)
            {
                MessageId = myPostPublicationResponse.MessageID;
                Console.WriteLine("Message " + MessageId + " has been pusblished!!\n");
            }
            else
            {
                Console.WriteLine(myPostPublicationResponse.StatusCode + " " + myPostPublicationResponse.ISBMHTTPResponse);
            }

        }
    }
}
