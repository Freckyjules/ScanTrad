namespace ScanTrad.Pipeline.Abstractions
{
    /// <summary>
    /// Les moteurs de traduction entre lesquels l'utilisateur peut choisir.
    /// </summary>
    /// <remarks>
    /// La liste est fermée et vit dans le contrat, pas dans les implémentations :
    /// c'est ce que le front affiche dans une liste déroulante et ce que l'API reçoit.
    /// Un énuméré plutôt qu'un <see cref="System.Type"/> ou une chaîne, parce qu'il se
    /// vérifie à la compilation et qu'il se documente tout seul.
    /// <para>
    /// Une valeur ici ne promet pas que le moteur soit installé : c'est
    /// <see cref="IFabriqueDeTraducteur.MoteursDisponibles"/> qui dit ce qui est
    /// réellement utilisable sur cette machine.
    /// </para>
    /// </remarks>
    public enum MoteurDeTraduction
    {
        /// <summary>
        /// OPUS-MT, modèle de traduction local dédié à une paire de langues. Petit et
        /// rapide, il tourne sur le processeur, mais traduit phrase par phrase sans
        /// mémoire d'une bulle à l'autre.
        /// </summary>
        OpusMt,

        /// <summary>
        /// NLLB-200, modèle multilingue de Meta. Nettement plus lent — mesuré huit fois
        /// le temps d'OPUS-MT par bulle, pour sept gigaoctets de modèle — mais il rend
        /// un français plus fidèle et digère le texte tout en majuscules sans aide.
        /// </summary>
        Nllb
    }
}
