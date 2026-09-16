using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Abstractions
{
    /// <summary>
    /// Réécrit le texte traduit dans les bulles nettoyées d'une planche. Dernière
    /// étape du pipeline de traitement d'image, après l'effacement.
    /// </summary>
    /// <remarks>
    /// C'est ici que la <em>composition finale</em> a lieu : le texte français de
    /// <see cref="ZoneDeTexte.TexteTraduit"/> vient se poser sur l'image déjà nettoyée
    /// par <see cref="IEffaceurDeTexte"/>. Les deux étapes sont volontairement
    /// séparées : corriger une traduction ne doit rejouer que celle-ci, jamais la
    /// détection ni l'effacement, qui n'ont pas changé.
    /// <para>
    /// Une zone sans bulle n'est pas réécrite, pour la même raison qu'elle n'est pas
    /// effacée : sans contour, il n'y a pas de place connue où poser le texte. La
    /// bulle ne sert qu'à cette décision, en revanche : le texte se pose dans
    /// <see cref="ZoneDeTexte.Rectangle"/> tel quel, pas dans le contour de la bulle.
    /// C'est le cadrage (<see cref="IAjusteurDeRectangle"/>), en amont, qui a la charge
    /// de maximiser ce rectangle dans la bulle ; la réécriture n'a pas à refaire ce
    /// travail ni à rétrécir une deuxième fois avec une marge à elle.
    /// </para>
    /// <para>
    /// Une zone dont <see cref="ZoneDeTexte.TexteTraduit"/> vaut <c>null</c> n'est pas
    /// réécrite non plus : c'est le signe qu'elle n'a pas encore été traduite, et non
    /// qu'il faille y écrire du vide. Une chaîne vide, elle, veut dire « traduite par
    /// rien » et n'est pas réécrite davantage — dans les deux cas, la bulle nettoyée
    /// reste telle quelle.
    /// </para>
    /// <para>
    /// L'implémentation ne modifie donc jamais la planche reçue : l'image nettoyée
    /// doit rester intacte, elle seule permet de rejouer la composition avec une
    /// traduction corrigée.
    /// </para>
    /// <para>
    /// Une zone à réécrire (bulle et traduction connues) sans
    /// <see cref="ZoneDeTexte.HauteurDeLigne"/> mesurée est un signe qu'une étape
    /// d'avant a un problème : l'implémentation lève plutôt que de deviner une taille
    /// de police à sa place.
    /// </para>
    /// </remarks>
    public interface IReecrivainDeTexte
    {
        /// <summary>
        /// Réécrit le texte traduit des zones dont la bulle est connue.
        /// </summary>
        /// <param name="planche">
        /// La planche nettoyée et ses zones. Son image sert de support au rendu ; les
        /// zones sans bulle ou sans traduction ne sont pas réécrites.
        /// </param>
        /// <returns>
        /// Une nouvelle planche portant l'image composée, avec les mêmes zones et le
        /// même sens de lecture. La planche reçue reste inchangée.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="planche"/> vaut <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Levée si l'image de la planche est vide ou n'est pas décodable.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Levée si une zone à réécrire n'a pas de <see cref="ZoneDeTexte.HauteurDeLigne"/> mesurée.
        /// </exception>
        Planche Reecrire(Planche planche);
    }
}
