namespace OpinionHub.Web.Models;
//  Класс варианта ответа
public class PollOption
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public Guid PollId { get; set; }
    public Poll? Poll { get; set; }
    // Коллекция с 
    public ICollection<VoteSelection> VoteSelections { get; set; } = new List<VoteSelection>();
    public string? ImagePath { get; set; }
}
