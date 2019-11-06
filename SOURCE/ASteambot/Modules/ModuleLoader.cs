using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.Modules
{
    public static class ModuleLoader
    {
        public static Module LoadASteambotModule(Assembly assembly)
        {
            try
            {
                //Loop through all types
                foreach (Type type in assembly.GetTypes())
                {
                    //Check for the type we actually want, in our case : ISteamChatHandler
                    if (type.GetInterfaces().Contains(typeof(ASteambotInterfaces.IASteambotChat)) || type.GetInterfaces().Contains(typeof(ASteambotInterfaces.IASteambotInventory)))
                    {
                        //Copy our class
                        object objectClass = Activator.CreateInstance(type);

                        //Create the new module object, wich contains infos and basic stuff
                        Module m = new Module(type, objectClass);

                        //Loop through all methodes, and add them to the module
                        foreach (MethodInfo mi in objectClass.GetType().GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public))
                            m.Methods.Add(new Method(m, mi));

                        return m;
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach(Exception e in ex.LoaderExceptions)
                    Program.PrintErrorMessage(e.ToString());
            }

            return null;
        }
    }
}
