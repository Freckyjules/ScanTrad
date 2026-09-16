using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Abstractions
{
    /// <summary>
    /// Agrandit le rectangle d'une zone jusqu'à la plus grande superficie qui tient
    /// dans le contour de sa bulle.
    /// </summary>
    /// <remarks>
    /// Le rectangle que rend la lecture est celui du détecteur, parfois complété par
    /// l'assemblage de plusieurs fragments (voir <see cref="ILecteurDePlanche"/>). Sur
    /// une bulle isolée, il reste en-deçà de ce que la bulle pourrait offrir. Sur deux
    /// bulles collées assemblées en une seule zone, il peut au contraire déborder de
    /// chacune d'elles — le rectangle englobant des fragments assemblés n'a aucune
    /// raison de rester dans le contour d'une bulle précise. Dans les deux cas, cette
    /// étape ne regarde que la bulle : le rectangle qui en ressort ne peut plus en
    /// sortir, et couvre la plus grande surface possible sans y déborder.
    /// <para>
    /// Une zone sans bulle n'est pas concernée : sans contour, il n'y a rien à
    /// maximiser, et le rectangle du détecteur reste tel quel.
    /// </para>
    /// <para>
    /// Comme <see cref="IOrdonnanceurDeZones"/> et <see cref="ITraducteur"/>,
    /// l'implémentation renseigne le résultat directement sur les zones reçues plutôt
    /// que d'en construire de nouvelles : rejouer cette étape est sans risque, elle ne
    /// dépend que du contour de la bulle, qui ne change pas d'un appel à l'autre.
    /// </para>
    /// </remarks>
    public interface IAjusteurDeRectangle
    {
        /// <summary>
        /// Maximise le rectangle de chaque zone dont la bulle est connue.
        /// </summary>
        /// <param name="planche">
        /// La planche et ses zones. Seules les zones portant une bulle sont
        /// modifiées ; l'image ne sert pas.
        /// </param>
        /// <returns>
        /// La même planche, dont les rectangles concernés ont été agrandis.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="planche"/> vaut <c>null</c>.
        /// </exception>
        Planche Ajuster(Planche planche);
    }
}
