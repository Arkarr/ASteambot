using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ASteambot.Modules
{
    public class Module
    {
        private Type type;
        public List<Method> Methods { get; private set; }
        public object Instance { get; private set; }
        public string Name { get; private set; }

        public Module(Type t, object instance)
        {
            this.type = t;
            this.Name = t.Name;
            this.Methods = new List<Method>();
            this.Instance = instance;
        }

        public Method GetMethodeByName(string name)
        {
            //Loop through all methods and get the one desired
            foreach (Method m in Methods)
            {
                if (m.MethodeInfo.Name == name)
                    return m;
            }

            //No methode found
            return null;
        }
        
        /// <summary>
        /// Attempte to run a methode using reflexion
        /// </summary>
        /// <param name="methodeName">Name of the methode</param>
        /// <param name="args">Arguments to pass to the methode</param>
        /// <returns>An object representing the result of the metode</returns>
        public object RunMethod(string methodeName, object[] args)
        {
            Method m = GetMethodeByName(methodeName);
            if (m != null && m.MethodeInfo.GetParameters().Count() != args.Count())
            {
                //ConsolePrinter.PrintMessage(Properties.Resources.ERROR_ArgumentsCountMismatch);
                Console.WriteLine(">>>>>>>>>> error methode args count");
                return null;
            }

            return Instance.GetType().GetMethod(methodeName).Invoke(Instance, args);
        }
    }
    
    public class Method
    {
        public MethodInfo MethodeInfo { get; private set; }
        public Module ModuleObj { get; private set; }

        public Method(Module ModuleObj, MethodInfo meth)
        {
            this.ModuleObj = ModuleObj;
            this.MethodeInfo = meth;
        }
    }
}
