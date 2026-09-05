using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Abstractions
{
    /// <summary>
    /// Efface le texte d'origine d'une planche pour laisser la place au texte
    /// traduit. Quatrième étape du pipeline, après la lecture, le regroupement et
    /// l'ordre.
    /// </summary>
    /// <remarks>
    /// La planche rendue porte l'<em>image nettoyée</em>, et c'est un livrable à
    /// stocker, pas un fichier temporaire. Toute la boucle de correction repose
    /// dessus : quand l'utilisateur retouche une traduction, on repart de l'image
    /// nettoyée et on ne rejoue que le rendu — quelques millisecondes au lieu de
    /// relancer la lecture et l'effacement sur la planche entière.
    /// <para>
    /// L'implémentation ne modifie donc jamais la planche reçue : l'image d'origine
    /// doit rester intacte, elle seule permet de tout reprendre si la détection s'est
    /// mal passée.
    /// </para>
    /// <para>
    /// Une zone sans bulle n'est pas effacée. C'est le cas des onomatopées dessinées
    /// à même la planche : on ne sait pas jusqu'où aller sans mordre sur le dessin,
    /// et elles ne sont de toute façon pas traduites en v1.
    /// </para>
    /// </remarks>
    public interface IEffaceurDeTexte
    {
        /// <summary>
        /// Efface le texte d'origine des zones dont la bulle est connue.
        /// </summary>
        /// <param name="planche">
        /// La planche et ses zones. Son image et les bulles de ses zones sont
        /// utilisées ; le texte, lui, ne sert pas.
        /// </param>
        /// <returns>
        /// Une nouvelle planche portant l'image nettoyée, avec les mêmes zones et le
        /// même sens de lecture. La planche reçue reste inchangée.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="planche"/> vaut <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Levée si l'image de la planche est vide ou n'est pas décodable.
        /// </exception>
        Planche Effacer(Planche planche);
    }
}
