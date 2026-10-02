namespace GeopoliticalMonitor.Application.Ucdp;

/// <summary>
/// Attribution required by UCDP. Citation from the UCDP Candidate codebook v1.5 ("When using this data, please always
/// cite"); acknowledgement and license from https://ucdp.uu.se/downloads/ (checked 2026-10-02).
/// </summary>
public static class UcdpAttribution
{
    public const string ProviderName = "Uppsala Conflict Data Program (UCDP)";
    public const string DatasetName = "UCDP Candidate Events Dataset";
    public const string Citation = "Hegre, Håvard, Mihai Croicu, Kristine Eck, and Stina Högbladh (July 2020). Introducing the UCDP Candidate Events Dataset. Research & Politics. doi:10.1177/2053168020935257";
    public const string Acknowledgement = "UCDP is part of and funded by DEMSCORE, national research infrastructure grant 2021-00162 from the Swedish Research Council.";
    public const string License = "Licensed under CC BY 4.0.";
    public const string RevisionCaveat = "Candidate data may be revised or removed in later UCDP releases.";
    public const string DownloadsPageUrl = "https://ucdp.uu.se/downloads/";
}
