using Libplanet;

namespace NineChronicles.Headless.GraphTypes
{
    public class Notification
    {
        public NotificationEnum Type { get; set; }
        public string Message { get; set; }
        
        public Address Receiver { get; }

        public Notification(NotificationEnum type, Address receiver)
        {
            Type = type;
            Receiver = receiver;
        }

        public Notification(NotificationEnum type, Address receiver, string msg) : this(type, receiver)
        {
            Message = msg;
        }
    }
}
