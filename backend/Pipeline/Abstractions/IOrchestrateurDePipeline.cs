using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Abstractions
{
    /// <summary>
    /// Enchaîne les six étapes du pipeline sur une planche brute et rend la planche
    /// composée, prête à être lue. Le point d'entrée du pipeline.
    /// </summary>
    /// <remarks>
    /// L'ordre est fixe : lecture, cadrage, ordre de lecture, traduction, effacement,
    /// réécriture. Le cadrage vient avant l'ordre de lecture parce que l'ordonnanceur
    /// trie sur le centre du rectangle de chaque zone, et c'est justement ce centre
    /// que le cadrage peut déplacer en maximisant le rectangle dans sa bulle — rejouer
    /// l'ordre sur des rectangles déjà cadrés est le seul enchaînement qui a du sens.
    /// La traduction vient après l'ordre parce qu'un dialogue en désordre se traduit
    /// mal (voir <see cref="ITraducteur"/>). L'effacement et la réécriture closent la
    /// composition, dans cet ordre parce que la seconde a besoin de l'image nettoyée
    /// que rend la première.
    /// <para>
    /// L'implémentation ne construit aucune de ces six étapes : elle les reçoit toutes
    /// déjà construites. C'est ce qui la rend testable sans le moindre modèle — un
    /// test peut lui passer six étapes factices et ne vérifier que l'enchaînement —
    /// et ce qui la laisse ignorer d'où vient un traducteur ou un chemin de modèle.
    /// Construire ces six étapes, choisir un moteur de traduction, décider où vivent
    /// les modèles : tout ça reste au-dessus de l'orchestrateur, chez celui qui
    /// l'assemble.
    /// </para>
    /// <para>
    /// Pour la même raison, l'orchestrateur ne possède ni ne libère le lecteur ni le
    /// traducteur qu'on lui donne, tous deux <see cref="IDisposable"/> : l'appelant qui
    /// les a construits en reste responsable, exactement comme s'il les utilisait
    /// directement.
    /// </para>
    /// <para>
    /// Ce n'est pas le seul chemin à travers le pipeline, seulement le plus direct.
    /// Corriger une traduction ne doit rejouer que <see cref="IReecrivainDeTexte"/>,
    /// pas les six étapes : cette boucle de correction passe par les étapes
    /// individuelles, pas par l'orchestrateur.
    /// </para>
    /// </remarks>
    public interface IOrchestrateurDePipeline
    {
        /// <summary>
        /// Traite une planche brute de bout en bout.
        /// </summary>
        /// <param name="planche">
        /// La planche de départ : son image et son sens de lecture, sans aucune zone.
        /// </param>
        /// <param name="jetonAnnulation">Jeton permettant d'interrompre le traitement.</param>
        /// <returns>
        /// La planche composée : texte traduit réécrit dans les bulles nettoyées.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="planche"/> vaut <c>null</c>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Levée si le traitement est interrompu via <paramref name="jetonAnnulation"/>.
        /// </exception>
        Task<Planche> TraiterAsync(Planche planche, CancellationToken jetonAnnulation = default);
    }
}
