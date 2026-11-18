using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

namespace SoBar.Widgets;

public sealed partial class WidgetService
{
    private void BuildAgenda(StackPanel body)
    {
        var saved = new ListBox { MaxHeight = 99, DisplayMemberPath = nameof(Appointment.Display), Margin = new Thickness(0,2,0,8) };
        AutomationProperties.SetName(saved, "Compromissos salvos");
        body.Children.Add(saved);
        var title = Field(body, "Compromisso", "");
        title.MaxLength = 100;
        var date = new DatePicker { SelectedDate = DateTime.Today, Margin = new Thickness(0,0,0,4) };
        AddFieldLabel(body, "Data", date);
        body.Children.Add(date);
        var hour = Field(body, "Hora local (HH:mm)", DateTime.Now.AddHours(1).ToString("HH:mm"));
        var status = Paragraph("");
        saved.SelectionChanged += (_,_) =>
        {
            if (saved.SelectedItem is not Appointment selected) return;
            title.Text = selected.Title;
            date.SelectedDate = selected.Start.LocalDateTime.Date;
            hour.Text = selected.Start.ToLocalTime().ToString("HH:mm");
        };
        void Reset()
        {
            saved.SelectedIndex = -1;
            title.Text = "";
            date.SelectedDate = DateTime.Today;
            hour.Text = DateTime.Now.AddHours(1).ToString("HH:mm");
        }
        void Update(Action? change)
        {
            try
            {
                change?.Invoke();
                saved.ItemsSource = agenda.All().ToArray();
                Reset();
                status.Text = change == null ? "" : "Agenda salva.";
                Refresh("agenda");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or
                InvalidOperationException or InvalidDataException or System.Text.Json.JsonException)
            { status.Text = ex.Message; }
        }
        var actions = new WrapPanel();
        body.Children.Add(actions);
        AddAction(actions, "Salvar", () => Update(() =>
        {
            if (date.SelectedDate is not DateTime selectedDay) throw new ArgumentException("Informe a data.");
            agenda.Save((saved.SelectedItem as Appointment)?.Id, title.Text, Agenda.LocalStart(selectedDay,hour.Text));
        }));
        AddAction(actions, "Novo", Reset);
        AddAction(actions, "Remover", () =>
        {
            if (saved.SelectedItem is not Appointment selected) { status.Text = "Escolha o compromisso a remover."; return; }
            if (MessageBox.Show(Window.GetWindow(body), $"Remover “{selected.Title}”?", "Agenda local",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
                Update(() => agenda.Remove(selected.Id));
        });
        body.Children.Add(status);
        Update(null);
    }
}
