using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Abstractions
{
    /// <summary>
    /// Rassemble en un seul bloc les zones de texte qui appartiennent à une même
    /// bulle. Deuxième étape du pipeline, juste après la lecture.
    /// </summary>
    /// <remarks>
    /// Les moteurs d'OCR détectent ligne par ligne : une bulle de trois lignes
    /// ressort en trois zones. Or une bulle porte une phrase — la traduire en trois
    /// morceaux séparés casse les accords et le temps, et le rendu français n'aura
    /// de toute façon ni le même nombre de lignes ni les mêmes coupures.
    /// <para>
    /// L'étape est séparée de la lecture parce qu'elle raisonne sur <em>l'ensemble</em>
    /// des zones d'une page et suppose de savoir comment se lit un manga — deux
    /// choses qu'un moteur d'OCR ignore.
    /// </para>
    /// <para>
    /// Le contrat ne dit pas <em>comment</em> rassembler, et surtout il ne demande pas
    /// à l'appelant de choisir : une page est mixte. Certaines zones ont une bulle,
    /// d'autres non, et la bonne façon de faire se décide zone par zone. Seule
    /// l'implémentation est en position de le savoir.
    /// </para>
    /// <para>
    /// Rien ici n'est asynchrone : ce n'est que de la géométrie, sans image, sans
    /// modèle et sans accès réseau.
    /// </para>
    /// </remarks>
    public interface IRegroupeurDeZones
    {
        /// <summary>
        /// Rassemble les zones d'une même bulle en un bloc unique.
        /// </summary>
        /// <param name="planche">
        /// La planche et ses zones, une par ligne détectée, telles que les rend un
        /// lecteur. Son image n'est pas utilisée.
        /// </param>
        /// <returns>
        /// Une nouvelle planche portant une zone par bloc reconstitué : le
        /// quadrilatère englobant de ses lignes, leurs textes mis bout à bout de haut
        /// en bas, la plus basse de leurs confiances, et leur bulle. Une zone qu'on
        /// n'a pas su rattacher — une onomatopée dessinée à même la planche, par
        /// exemple — ressort seule.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Levée si <paramref name="planche"/> vaut <c>null</c>.
        /// </exception>
        Planche Regrouper(Planche planche);
    }
}
