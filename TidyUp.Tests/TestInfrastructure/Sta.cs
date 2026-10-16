namespace TidyUp.Tests.TestInfrastructure;

internal static class Sta
{
    /// <summary>Runs <paramref name="action"/> on an STA thread (WPF resources require it) and rethrows any failure.</summary>
    public static void Run(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
