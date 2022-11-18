using System.Collections.Generic;
using MongoDB.Driver;

namespace YouTubeHelper.Shared
{
    public interface IHasIdentifier<T>
    {
        T Id { get; set; }
    }

    public static class MongoDbExtensions
    {
        public static bool Upsert<TObject, TId>(this IMongoCollection<TObject> collection, TObject obj)
            where TObject : IHasIdentifier<TId>
        {
            if (EqualityComparer<TId>.Default.Equals(obj.Id, default))
            {
                // If the ID has the default value, then we always want to do an insert
                // and let Mongo create the ID and assign it back to the C# object.
                collection.InsertOne(obj);
                return true;
            }
            else
            {
                // If the ID is not empty, that's still no guarantee that the object is in the db
                // (since we might have generated the id programmatcally)
                // Therefore, we need to do an upsert (rather than something like a replace).
                var result = collection.ReplaceOne(o => o.Id.Equals(obj.Id), obj, new ReplaceOptions { IsUpsert = true });
                return result.ModifiedCount > 0;
            }
        }

        public static void Delete<TObject, TId>(this IMongoCollection<TObject> collection, TId id)
            where TObject : IHasIdentifier<TId>
        {
            collection.DeleteOne(Builders<TObject>.Filter.Eq("_id", id));
        }

        // Note that this replaces the whole object, so it's not good for limited property list updated
        public static void Update<TObject, TId>(this IMongoCollection<TObject> collection, TObject obj)
            where TObject : IHasIdentifier<TId>
        {
            collection.ReplaceOne(Builders<TObject>.Filter.Eq("_id", obj.Id), obj);
        }

        public static IEnumerable<TObject> FindAll<TObject>(this IMongoCollection<TObject> collection)
        {
            return collection.Find(_ => true).ToEnumerable();
        }

        public static TObject FindById<TObject, TId>(this IMongoCollection<TObject> collection, TId id)
        {
            return collection.Find(Builders<TObject>.Filter.Eq("_id", id)).FirstOrDefault();
        }
    }
}
