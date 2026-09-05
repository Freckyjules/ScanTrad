using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Abstractions
{
    /// <summary>
    /// Traduit en français le texte des zones d'une planche. Troisième étape du
    /// pipeline, après l'ordonnancement.
    /// </summary>
    /// <remarks>
    /// La traduction porte sur la <b>planche entière</b> et non sur une zone. Ce n'est
    /// pas une commodité : les répliques d'une page forment un dialogue, et une bulle
    /// isolée n'a pas de quoi choisir un temps, un registre, ni même le genre d'un
    /// pronom. Une implémentation qui traduit phrase par phrase reste libre de le
    /// faire, mais elle ne doit pas y être forcée par le contrat.
    /// <para>
    /// C'est aussi pourquoi cette étape vient après l'ordonnancement : les zones
    /// arrivent dans l'ordre de lecture, et un dialogue en désordre se traduit mal.
    /// </para>
    /// <para>
    /// <b>Aucune mémoire cachée.</b> Une implémentation ne doit pas accumuler en
    /// secret ce qu'elle a vu des planches précédentes. Toute la boucle de correction
    /// repose sur le fait qu'une étape se rejoue : un traducteur qui se souviendrait
    /// en privé rendrait deux résultats différents pour la même planche, et l'ordre
    /// des appels deviendrait significatif sans que personne ne l'ait décidé.
    /// </para>
    /// <para>
    /// Ce qui doit traverser les planches — les noms propres, le tutoiement, le
    /// registre d'un personnage — se transmet donc <em>explicitement</em>, par
    /// exemple un glossaire confié à la construction de l'implémentation. L'appelant
    /// en reste propriétaire, il peut le sauvegarder, le relire, et l'utilisateur peut
    /// le corriger : décider une fois pour toutes comment se traduit un nom est une
    /// fonctionnalité, pas un détail d'implémentation.
    /// </para>
    /// <para>
    /// Le contrat impose <see cref="IDisposable"/> pour la même raison que la lecture :
    /// celui qui l'utilise ne peut pas savoir ce qu'il y a derrière. Un modèle local
    /// retient de la mémoire native qu'aucun ramasse-miettes ne libérera, là où un
    /// service distant ne retient rien et écrit un <c>Dispose</c> vide.
    /// </para>
    /// </remarks>
    public interface ITraducteur : IDisposable
    {
        /// <summary>
        /// Traduit les zones de la planche et renseigne leur
        /// <see cref="ZoneDeTexte.TexteTraduit"/>.
        /// </summary>
        /// <param name="planche">
        /// La planche et ses zones, dans l'ordre de lecture. Seuls les textes sont
        /// utilisés ; l'image ne sert pas.
        /// <para>
        /// Une zone dont <see cref="ZoneDeTexte.TexteTraduit"/> est déjà renseigné
        /// n'est <b>pas</b> retraduite : c'est ce qui rend sûr le fait de relancer
        /// l'étape sur une planche que l'utilisateur a déjà corrigée. Pour obtenir une
        /// traduction neuve, on remet le champ à <c>null</c> avant d'appeler.
        /// </para>
        /// </param>
        /// <param name="jetonAnnulation">Jeton permettant d'interrompre le traitement.</param>
        /// <returns>
        /// Une nouvelle planche portant les mêmes zones, traduites. La planche reçue
        /// reste inchangée.
        /// <para>
        /// Une zone que le traducteur n'a pas su traduire garde un texte traduit à
        /// <c>null</c> : c'est le signe qu'il reste du travail, là où une chaîne vide
        /// voudrait dire « traduit par rien, ne rien afficher ».
        /// </para>
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="planche"/> vaut <c>null</c>.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Levée si le traitement est interrompu via <paramref name="jetonAnnulation"/>.
        /// </exception>
        Task<Planche> TraduireAsync(Planche planche, CancellationToken jetonAnnulation = default);
    }
}
