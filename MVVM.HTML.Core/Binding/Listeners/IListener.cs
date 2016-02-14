using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVVM.HTML.Core.Binding.Listeners
{
    public interface IListener
    {
        void Listen();

        void UnListen();
    }
}
