using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace MudBlazorTreeSelect.Tests
{
    /// <summary>
    /// Creates a bUnit context with MudBlazor's services for each test.
    /// </summary>
    public abstract class TreeSelectTestBase
    {
        protected BunitContext Context { get; private set; } = null!;

        [SetUp]
        public virtual void Setup()
        {
            Context = new BunitContext();
            Context.JSInterop.Mode = JSRuntimeMode.Loose;
            Context.Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Context.Dispose();
            }
            catch (Exception)
            {
                // Disposing a context with pending JS interop can throw, which does not matter after the test.
            }
        }
    }
}
