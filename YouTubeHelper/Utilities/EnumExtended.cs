using System;
using System.ComponentModel;
using System.Linq;

namespace YouTubeHelper.Utilities
{
    public class EnumExtended<T> where T : Enum
    {
        public EnumExtended(T value)
        {
            Value = value;
        }

        public T Value { get; }

        public string Description => typeof(T).GetMember(Value.ToString()).FirstOrDefault().GetCustomAttributes(typeof(DescriptionAttribute), true).OfType<DescriptionAttribute>().FirstOrDefault()?.Description;
    }
}
