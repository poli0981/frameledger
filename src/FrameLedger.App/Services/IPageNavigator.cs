namespace FrameLedger.App.Services;

/// <summary>The two navigation moves a view model makes, behind an interface so a test can watch them without a <c>NavigationView</c>.</summary>
public interface IPageNavigator
{
    void Navigate<TPage>() where TPage : class;

    void GoBack();
}
