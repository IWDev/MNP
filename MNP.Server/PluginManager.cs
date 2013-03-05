using MNP.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Text;

namespace MNP.Server
{
    internal class PluginManager
    {
        internal PluginManager()
        {
             //An aggregate catalog that combines multiple catalogs
            var catalog = new AggregateCatalog();
            
            //Adds all the parts found in same directory where the application is running!
            var currentPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(PluginManager)).Location);
            catalog.Catalogs.Add(new DirectoryCatalog(@"C:\plugins\"));

            //Create the CompositionContainer with the parts in the catalog
            var _container = new CompositionContainer(catalog);

            //Fill the imports of this object
            try
            {
                _container.ComposeParts(this);
            }
            catch (CompositionException compositionException)
            {
                Console.WriteLine(compositionException.ToString());
            }

            //Prints all the languages that were found into the application directory
            //var i = 0;
            //foreach (var plugin in this.availablePlugins)
            //{
            //    Console.WriteLine(plugin.Description);
            //    i++;
            //}
            //Console.WriteLine("{0} Plugins have been detected and loaded.",i);

        }

        //[ImportMany]
        //IEnumerable<IPlugin> availablePlugins { get; set; }



    }
}
