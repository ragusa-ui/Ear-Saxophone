using System.Windows.Threading;
using EarSaxophone.Core.ViewModels;

namespace EarSaxophone.App;

internal sealed class DispatcherUiThread(Dispatcher dispatcher) : IUiThread
{
    public void Post(Action action) => dispatcher.BeginInvoke(action);
}
