using LiteDB;

namespace YouTubeHelper.Models
{
    public class Channel
    {
        [BsonId]
        public int ObjectId { get; set; }
        
        public string Name { get; set; }
    }
}
