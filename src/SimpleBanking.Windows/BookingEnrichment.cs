namespace SimpleBanking;

internal static class BookingEnrichment
{
    internal static BookingAttachment ReadAttachment(string path){
        var info=new FileInfo(path);if(info.Length>3*1024*1024)throw new InvalidDataException("Maximal 3 MB pro Anhang erlaubt.");
        string extension=Path.GetExtension(path).ToLowerInvariant();if(extension is not (".pdf" or ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp"))throw new InvalidDataException("Bitte eine PDF- oder Bilddatei auswählen.");
        var bytes=File.ReadAllBytes(path);if(extension==".pdf"){if(bytes.Length<5||System.Text.Encoding.ASCII.GetString(bytes,0,5)!="%PDF-")throw new InvalidDataException("Keine gültige PDF-Datei.");}
        else {using var stream=new MemoryStream(bytes);using var image=Image.FromStream(stream);if(image.Width>20000||image.Height>20000)throw new InvalidDataException("Bildabmessungen zu groß.");}
        return new(Guid.NewGuid().ToString("N"),Path.GetFileName(path),bytes);
    }
    internal static Entry Preserve(Entry current,Entry old)=>current with {Category=old.Category,CategoryManual=old.CategoryManual,Note=old.Note,Read=old.Read,Flagged=old.Flagged,Attachments=old.Attachments,ReminderAt=old.ReminderAt,ReminderDone=old.ReminderDone,ToDo=old.ToDo,CustomLogo=old.CustomLogo,MerchantOverride=old.MerchantOverride};
    // Match each old occurrence at most once. Ambiguous matches are left untouched rather than moving attachments to the wrong booking.
    internal static List<Entry> Reconcile(IEnumerable<Entry> oldEntries,IEnumerable<Entry> incoming,ICollection<Entry>? unmatched=null){
        var available=oldEntries.ToList();var output=new List<Entry>();
        foreach(var current in incoming){
            var candidates=available.Where(e=>e.AccountId==current.AccountId&&e.Currency==current.Currency&&e.Id==current.Id).ToArray();
            if(candidates.Length!=1&&current.EndToEndId.Length>0&&current.EndToEndId!="NOTPROVIDED")candidates=available.Where(e=>e.AccountId==current.AccountId&&e.Currency==current.Currency&&e.EndToEndId==current.EndToEndId&&e.Amount==current.Amount).ToArray();
            if(candidates.Length!=1)candidates=available.Where(e=>e.AccountId==current.AccountId&&e.Currency==current.Currency&&e.Amount==current.Amount&&(e.OriginalMerchant??e.Merchant)==(current.OriginalMerchant??current.Merchant)&&e.Purpose==current.Purpose&&(e.Date==current.Date||e.Pending&&!current.Pending&&Math.Abs(e.Date.DayNumber-current.Date.DayNumber)<=7)).ToArray();
            if(candidates.Length==1){output.Add(Preserve(current,candidates[0]));available.Remove(candidates[0]);}else output.Add(current);
        }if(unmatched!=null)foreach(var item in available.Where(e=>e.Note.Length>0||e.Attachments?.Count>0||e.ReminderAt!=null||e.CustomLogo!=null||e.CategoryManual||e.MerchantOverride!=null))unmatched.Add(item);return output;
    }
}

internal sealed partial class TransactionDetails
{
    void EditReminder(){
        using var dialog=new Form{Text="Erinnerung",ClientSize=new Size(330,126),FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,MaximizeBox=false,MinimizeBox=false,Font=Font};
        var when=new DateTimePicker{Bounds=new Rectangle(16,16,298,28),Format=DateTimePickerFormat.Custom,CustomFormat="dd.MM.yyyy HH:mm",Value=entry.ReminderAt??DateTime.Today.AddDays(1).AddHours(9)};dialog.Controls.Add(when);
        void Commit(DateTime? at,bool done){var changed=entry with{ReminderAt=at,ReminderDone=done};save(changed);entry=changed;dialog.DialogResult=DialogResult.OK;}
        var apply=new Button{Text="Speichern",Bounds=new Rectangle(214,72,100,30)};apply.Click+=(_,_)=>Commit(when.Value,false);dialog.Controls.Add(apply);dialog.AcceptButton=apply;
        if(entry.ReminderAt!=null){var done=new Button{Text=entry.ReminderDone?"Aktivieren":"Erledigt",Bounds=new Rectangle(16,72,92,30)};done.Click+=(_,_)=>Commit(entry.ReminderAt,!entry.ReminderDone);dialog.Controls.Add(done);var remove=new Button{Text="Entfernen",Bounds=new Rectangle(114,72,94,30)};remove.Click+=(_,_)=>Commit(null,false);dialog.Controls.Add(remove);}
        if(reminderStatus!=null){dialog.ClientSize=new(330,158);dialog.Controls.Add(new Label{Text=reminderStatus(),AutoSize=true,MaximumSize=new(298,0),Location=new(16,116),ForeColor=ThemeStyling.Current.Secondary});}
        dialog.ShowDialog(this);
    }
    void AddEnrichmentCards(FlowLayoutPanel content){
        var attachments=new RoundedCard{Width=352,AllowDrop=true};content.Controls.Add(attachments);
        void AddFiles(string[] paths){try{var list=(entry.Attachments??[]).ToList();if(list.Count+paths.Length>3)throw new InvalidDataException("Maximal 3 Anhänge pro Buchung erlaubt.");var newItems=paths.Select(BookingEnrichment.ReadAttachment).ToArray();list.AddRange(newItems);var changed=entry with{Attachments=list};save(changed);entry=changed;Render();}catch(Exception ex) when(ex is IOException or InvalidDataException or ArgumentException or OutOfMemoryException){MessageBox.Show(this,ex.Message,"Anhang nicht hinzugefügt");}}
        void Render(){foreach(Control control in attachments.Controls.Cast<Control>().ToArray())control.Dispose();if(entry.Attachments?.Count>0)attachments.Controls.Add(DashboardWindow.Label($"Anhänge · {entry.Attachments.Count}/3",12,true));
            foreach(var item in entry.Attachments??[]){attachments.Controls.Add(DashboardWindow.Label(item.Name+" · "+(item.Content.Length/1024)+" KB",12));var actions=new FlowLayoutPanel{AutoSize=true,Width=330};actions.Controls.Add(DashboardWindow.ActionButton("Speichern unter …",()=>{using var dialog=new SaveFileDialog{FileName=Path.GetFileName(item.Name),Filter="Datei|*.*"};if(dialog.ShowDialog(this)==DialogResult.OK)File.WriteAllBytes(dialog.FileName,item.Content);}));actions.Controls.Add(DashboardWindow.ActionButton("Entfernen",()=>{var changed=entry with{Attachments=(entry.Attachments??[]).Where(a=>a.Id!=item.Id).ToList()};save(changed);entry=changed;Render();}));attachments.Controls.Add(actions);}
            var add=new FluentButton{Text="Anhang hinzufügen",TextAlign=ContentAlignment.MiddleLeft,Width=320,Height=36,Margin=Padding.Empty,Image=ExportDesign.Icon("attach_20_regular-444")};add.Disposed+=(_,_)=>add.Image?.Dispose();add.Click+=(_,_)=>{using var dialog=new OpenFileDialog{Filter="PDF und Bilder|*.pdf;*.png;*.jpg;*.jpeg;*.gif;*.bmp",Multiselect=true};if(dialog.ShowDialog(this)==DialogResult.OK)AddFiles(dialog.FileNames);};attachments.Controls.Add(add);}
        attachments.DragEnter+=(_,e)=>e.Effect=e.Data?.GetDataPresent(DataFormats.FileDrop)==true?DragDropEffects.Copy:DragDropEffects.None;
        attachments.DragDrop+=(_,e)=>{if(e.Data?.GetData(DataFormats.FileDrop) is string[] paths)AddFiles(paths);};Render();
    }
}
