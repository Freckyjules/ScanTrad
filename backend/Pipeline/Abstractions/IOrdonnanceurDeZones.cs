using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Abstractions
{
    /// <summary>
    /// Met les blocs de texte d'une planche dans l'ordre où un lecteur les lit.
    /// Deuxième étape du pipeline.
    /// </summary>
    /// <remarks>
    /// L'ordre compte pour deux raisons : le traducteur a besoin des répliques dans
    /// l'ordre du dialogue pour tenir le fil d'une conversation, et le front doit
    /// présenter les corrections dans un ordre qui suive la lecture.
    /// <para>
    /// Le contrat porte sur la planche entière et non sur deux zones à comparer. Ce
    /// n'est pas une précaution de style : l'ordre dépend du sens de lecture, qui est
    /// une propriété de la planche, et une implémentation peut légitimement vouloir
    /// regarder l'ensemble de la mise en page pour trancher. Ce qu'elle regarde
    /// vraiment ne regarde qu'elle — un tri par position suffit, une analyse des
    /// cases est permise.
    /// </para>
    /// <para>
    /// Aucune implémentation ne rendra un ordre juste à tous les coups : certaines
    /// mises en page ne se laissent pas retrouver depuis la seule position des
    /// bulles. Le rang est donc une proposition, 
    /// et non une vérité sur laquelle la suite du pipeline peut s'appuyer les
    /// yeux fermés.
    /// </para>
    /// <para>
    /// Rien ici n'est asynchrone : ce n'est que de la géométrie, sans image, sans
    /// modèle et sans accès réseau.
    /// </para>
    /// </remarks>
    public interface IOrdonnanceurDeZones
    {
        /// <summary>
        /// Met les zones dans l'ordre de lecture et renseigne leur
        /// <see cref="ZoneDeTexte.OrdreDeLecture"/>.
        /// </summary>
        /// <param name="planche">
        /// La planche et ses blocs, tels que les rend un lecteur. Son
        /// <see cref="Planche.Sens"/> décide par quel côté on commence ; son image
        /// n'est pas utilisée.
        /// </param>
        /// <returns>
        /// Une nouvelle planche portant les mêmes zones, rangées dans l'ordre de
        /// lecture. Chacune porte désormais son rang, à partir de zéro.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="planche"/> vaut <c>null</c>.
        /// </exception>
        Planche Ordonner(Planche planche);
    }
}
