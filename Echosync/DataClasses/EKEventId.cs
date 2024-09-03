using Echosync.Enums;

namespace Echosync.DataClasses
{
    public class EKEventId
    {
        public int Id { get; set; }
        public TextSource textSource { get; set; }

        public EKEventId(int id, TextSource textSource)
        {
            Id = id;
            this.textSource = textSource;
        }
    }
}
