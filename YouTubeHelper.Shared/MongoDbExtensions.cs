using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace YouTubeHelper.Shared
{
    public interface IHasIdentifier<T>
    {
        T Id { get; init; }
    }

    public static class MongoDbExtensions
    {
        public static async Task<bool> UpsertAsync<TObject, TId>(this IMongoCollection<TObject> collection, TObject obj)
            where TObject : IHasIdentifier<TId>
        {
            if (EqualityComparer<TId>.Default.Equals(obj.Id, default))
            {
                // If the ID has the default value, then we always want to do an insert
                // and let Mongo create the ID and assign it back to the C# object.
                await collection.InsertOneAsync(obj);
                return true;
            }
            else
            {
                // If the ID is not empty, that's still no guarantee that the object is in the db
                // (since we might have generated the id programmatcally)
                // Therefore, we need to do an upsert (rather than something like a replace).
                ReplaceOneResult? result = await collection.ReplaceOneAsync(o => o.Id!.Equals(obj.Id), obj, new ReplaceOptions { IsUpsert = true });
                return result.ModifiedCount > 0;
            }
        }

        [Obsolete("Use in favor of UpsertAsync if possible")]
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
                var result = collection.ReplaceOne(o => o.Id!.Equals(obj.Id), obj, new ReplaceOptions { IsUpsert = true });
                return result.ModifiedCount > 0;
            }
        }

        public static async Task DeleteAsync<TObject, TId>(this IMongoCollection<TObject> collection, TId id)
            where TObject : IHasIdentifier<TId>
        {
            await collection.DeleteOneAsync(Builders<TObject>.Filter.Eq("_id", id));
        }

        // Note that this replaces the whole object, so it's not good for limited property list updated
        public static async Task UpdateAsync<TObject, TId>(this IMongoCollection<TObject> collection, TObject obj)
            where TObject : IHasIdentifier<TId>
        {
            await collection.ReplaceOneAsync(Builders<TObject>.Filter.Eq("_id", obj.Id), obj);
        }

        [Obsolete("Use in favor of FindAllAsync if possible")]
        public static List<TObject> FindAll<TObject>(this IMongoCollection<TObject> collection)
        {
            return collection.Find(_ => true).ToList();
        }

        public static async Task<List<TObject>> FindAllAsync<TObject>(this IMongoCollection<TObject> collection)
        {
            return await (await collection.FindAsync(_ => true)).ToListAsync();
        }

        public static TObject FindById<TObject, TId>(this IMongoCollection<TObject> collection, TId id)
        {
            return collection.Find(Builders<TObject>.Filter.Eq("_id", id)).FirstOrDefault();
        }

        public static async Task<TObject> FindByIdAsync<TObject, TId>(this IMongoCollection<TObject> collection, TId id)
        {
            return (await collection.FindAsync(Builders<TObject>.Filter.Eq("_id", id))).FirstOrDefault();
        }

        public static async Task<List<TObject>> FindByConditionAsync<TObject>(this IMongoCollection<TObject> collection, Expression<Func<TObject, bool>> filter)
        {
            return await (await collection.FindAsync<TObject>(filter)).ToListAsync();
        }
    }
}
