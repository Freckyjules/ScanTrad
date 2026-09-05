using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Abstractions
{
    /// <summary>
    /// Met les blocs de texte d'une planche dans l'ordre où un lecteur les lit.
    /// Troisième étape du pipeline, après la lecture et le regroupement.
    /// </summary>
    /// <remarks>
    /// Ce n'est pas un tri, et le nom du contrat le dit exprès. Un tri suppose qu'on
    /// puisse répondre à « A vient-il avant B ? » en ne regardant que A et B. Ici
    /// c'est impossible : deux bulles aux mêmes positions relatives s'ordonnent
    /// différemment selon le découpage en cases qui les entoure. Une bulle plus basse
    /// et plus à gauche passe avant si elle est dans une bande supérieure, après si
    /// elle est dans la même bande. Aucun comparateur ne peut trancher.
    /// <para>
    /// L'ordre compte pour deux raisons : le traducteur a besoin des répliques dans
    /// l'ordre du dialogue pour tenir le fil d'une conversation, et le front doit
    /// présenter les corrections dans un ordre qui suive la lecture.
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
        /// <param name="zones">
        /// Les blocs d'une même planche, tels que les rend un regroupeur.
        /// </param>
        /// <returns>
        /// Les mêmes zones, rendues dans l'ordre de lecture. Chacune porte désormais
        /// son rang, à partir de zéro.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="zones"/> vaut <c>null</c>.
        /// </exception>
        IReadOnlyList<ZoneDeTexte> Ordonner(IReadOnlyList<ZoneDeTexte> zones);
    }
}
