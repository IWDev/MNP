using MNP.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MNP.PluginSample
{
    [Export(typeof(IPlugin))]
    public class Class1 : IPlugin
    {
        public string Description
        {
            get { return "Hello World";  }
        }
    }
}
