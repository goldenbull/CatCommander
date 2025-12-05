using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Metalama.Patterns.Observability;
using NLog;

namespace Experiments;

[Observable]
public class MyViewModel
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    public int Counter { get; set; } = 1;
    public ObservableCollection<int> Numbers { get; set; } = [1, 2, 3, 4, 5, 6, 7, 8, 9];

    public void ClickMe()
    {
        Counter++;
        Numbers.Add(Counter);
    }

    public void RunBackground()
    {
        Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < 100; i++)
                {
                    Counter++;
                    Numbers.Add(Counter);
                    Task.Delay(100).Wait();
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        });
    }
}