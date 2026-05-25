using System.Windows.Input;

namespace StatsClient.MVVM.Core;

public class ActionCommand<T> : ICommand
{
    public event EventHandler CanExecuteChanged;
    private Action<T> _action;

    public ActionCommand(Action<T> action)
    {
        _action = action;
    }

    public bool CanExecute(object parameter) { return true; }

    public void Execute(object parameter)
    {
        if (_action != null)
        {
            var castParameter = (T)Convert.ChangeType(parameter, typeof(T));
            _action(castParameter);
        }
    }
}

/*


EventToCommandBehavior.cs is in Core folder..


How to Use:
 
XAML:

<i:Interaction.Behaviors>
    <core:EventToCommandBehavior Command="{Binding KeyPressOnThreeShapeListViewCommand}"
                                 Event="KeyDown"
                                 PassArguments="True" />
</i:Interaction.Behaviors>
 


ViewModel:

public ActionCommand<KeyEventArgs> KeyPressOnThreeShapeListViewCommand { get; private set; }



KeyPressOnThreeShapeListViewCommand = new ActionCommand<KeyEventArgs>(KeyPressOnThreeShapeListView);


private void KeyPressOnThreeShapeListView(KeyEventArgs e)
{
    Debug.WriteLine(e.Key);
}
 
 */
