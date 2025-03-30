using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using DiscordBotBase;

namespace DenizenBot
{
    public abstract class Extension
    {
        public static List<Extension> Extensions;

        public static void Init()
        {
            if (Extensions != null)
            {
                return;
            }
            Extensions = [.. Assembly.GetExecutingAssembly().DefinedTypes.Where(t => t.IsSubclassOf(typeof(Extension))).Select(t => t.AsType().GetConstructor([]).Invoke(null) as Extension)];
        }

        public virtual void OnClientInit(DiscordBot bot)
        {
        }
    }
}
