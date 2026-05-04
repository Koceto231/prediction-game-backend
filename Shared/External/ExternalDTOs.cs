namespace BPFL.API.Shared.External
{
    public class CompetiotionDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Code { get; set; } = null!;
    }

    public class CompetitionResponseDto
    {
        public int Count { get; set; }
        public List<CompetiotionDTO> Competitions { get; set; } = null!;
    }

    public class ExternalFullTimeDTO
    {
        public int? Home { get; set; }
        public int? Away { get; set; }
    }

    public class ExternalScoreDTO
    {
        public ExternalFullTimeDTO FullTime { get; set; } = null!;
    }

    public class ExternalTeamRefDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    public class ExternalMatchDTO
    {
        public int Id { get; set; }
        public DateTime UtcDate { get; set; }
        public string Status { get; set; } = null!;
        public int MatchDay { get; set; }
        public ExternalTeamRefDTO HomeTeam { get; set; } = null!;
        public ExternalTeamRefDTO AwayTeam { get; set; } = null!;
        public ExternalScoreDTO Score { get; set; } = null!;
    }

    public class ExternalMatchDetailDTO
    {
        public int Id { get; set; }
        public string Status { get; set; } = null!;
        public List<ExternalGoalDTO> Goals { get; set; } = new();
        public List<ExternalBookingDTO> Bookings { get; set; } = new();
    }

    public class ExternalGoalDTO
    {
        public ExternalPersonRefDTO? Scorer { get; set; }
        public ExternalPersonRefDTO? Assist { get; set; }
    }

    public class ExternalBookingDTO
    {
        public ExternalPersonRefDTO? Player { get; set; }
        public string Card { get; set; } = null!;
    }

    public class ExternalPersonRefDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
    }

    public class ExternalSquadPlayerDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Position { get; set; }
    }

    public class ExternalTeamDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string ShortName { get; set; } = null!;
        public string Crest { get; set; } = null!;
        public List<ExternalSquadPlayerDTO> Squad { get; set; } = new();
    }

    public class MatchesResponseDTO
    {
        public List<ExternalMatchDTO> Matches { get; set; } = new();
    }

    public class TeamResponseDto
    {
        public int Count { get; set; }
        public List<ExternalTeamDTO> Teams { get; set; } = null!;
    }
}
