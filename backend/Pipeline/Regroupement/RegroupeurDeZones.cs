using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.Regroupement
{
    /// <summary>
    /// Rassemble les lignes d'une même bulle en un seul bloc de texte.
    /// </summary>
    /// <remarks>
    /// Une page est mixte : sur une planche d'essai, 45 zones sur 61 avaient une
    /// bulle et les autres non. La stratégie se décide donc zone par zone, et non une
    /// fois pour toute la page.
    /// <para>
    /// Là où une bulle est connue, elle fait foi : le dessinateur a déjà fait le
    /// travail de regroupement, il suffit de le lire, et il n'y a aucun seuil à
    /// régler. Là où il n'y en a pas, on se rabat sur la géométrie.
    /// </para>
    /// <para>
    /// Le rassemblement par bulle se fait sur la <em>géométrie</em> et non sur
    /// l'identité des objets : un lecteur fabrique une bulle par ligne détectée, donc
    /// deux lignes d'une même bulle à l'écran portent deux objets distincts de même
    /// forme. On regarde donc si le centre d'une ligne tombe dans la bulle d'une
    /// autre, ce que <see cref="Bulle.Contient"/> sait faire.
    /// </para>
    /// </remarks>
    public class RegroupeurDeZones : IRegroupeurDeZones
    {
        #region Méthodes

        /// <inheritdoc />
        public IReadOnlyList<ZoneDeTexte> Regrouper(IReadOnlyList<ZoneDeTexte> zones)
        {
            if (zones == null)
            {
                throw new ArgumentNullException(nameof(zones));
            }

            List<List<ZoneDeTexte>> blocs = new List<List<ZoneDeTexte>>();
            List<ZoneDeTexte> sansBulle = new List<ZoneDeTexte>();

            foreach (ZoneDeTexte zone in zones)
            {
                if (zone.Bulle == null)
                {
                    sansBulle.Add(zone);
                    continue;
                }

                List<ZoneDeTexte>? bloc = TrouverLeBlocAccueillant(blocs, zone);

                if (bloc == null)
                {
                    blocs.Add(new List<ZoneDeTexte> { zone });
                }
                else
                {
                    bloc.Add(zone);
                }
            }

            List<ZoneDeTexte> resultat = new List<ZoneDeTexte>();

            foreach (List<ZoneDeTexte> bloc in blocs)
            {
                resultat.Add(Fusionner(bloc));
            }

            resultat.AddRange(RassemblerSansBulle(sansBulle));

            return resultat;
        }

        #endregion

        #region Méthodes privées

        private static List<ZoneDeTexte> RassemblerSansBulle(List<ZoneDeTexte> zones)
        {
            // Repli pour les zones dont aucune bulle n'est connue. Aujourd'hui chacune
            // ressort seule, ce qui est juste pour une onomatopée dessinée à même la
            // planche — le cas de loin le plus fréquent. Un rassemblement géométrique,
            // par proximité et alignement, viendra ici quand un cartouche sans contour
            // posera problème.
            return new List<ZoneDeTexte>(zones);
        }

        private static List<ZoneDeTexte>? TrouverLeBlocAccueillant(
            List<List<ZoneDeTexte>> blocs,
            ZoneDeTexte zone)
        {
            Coordonnee centre = zone.Quadrilatere.Centre;

            foreach (List<ZoneDeTexte> bloc in blocs)
            {
                // La bulle de la première zone du bloc fait référence pour tout le bloc.
                Bulle bulleDuBloc = bloc[0].Bulle!;

                if (bulleDuBloc.Contient(centre))
                {
                    return bloc;
                }
            }

            return null;
        }

        private static ZoneDeTexte Fusionner(List<ZoneDeTexte> bloc)
        {
            // De haut en bas d'abord : dans une bulle, les lignes s'empilent. La
            // gauche-droite ne départage que deux fragments à la même hauteur. À ne
            // pas confondre avec l'ordre de lecture des bulles sur la page, qui lui
            // va de droite à gauche — mais c'est le sujet d'une autre étape.
            List<ZoneDeTexte> ordonnees = bloc
                .OrderBy(zone => zone.Quadrilatere.Centre.Y)
                .ThenBy(zone => zone.Quadrilatere.Centre.X)
                .ToList();

            ZoneDeTexte fusionnee = new ZoneDeTexte();

            fusionnee.Quadrilatere = EnglobantOriente(ordonnees);

            // Les lignes d'une bulle sont les morceaux d'une même phrase : on les
            // recolle avec une espace, pas un retour à la ligne. Le rendu français
            // recoupera lui-même là où il faut, et pas au même endroit.
            fusionnee.TexteOriginal = string.Join(" ", ordonnees.Select(zone => zone.TexteOriginal.Trim()));

            // Le bloc ne vaut que ce que vaut sa ligne la moins sûre.
            fusionnee.Confiance = ordonnees.Min(zone => zone.Confiance);

            // La bulle du bloc, celle qui a servi à le constituer.
            fusionnee.Bulle = bloc[0].Bulle;

            return fusionnee;
        }

        private static Quadrilatere EnglobantOriente(List<ZoneDeTexte> bloc)
        {
            double angle = AngleMoyen(bloc);
            double radians = angle * Math.PI / 180;

            // On se place dans le repère du texte pour y prendre un rectangle droit,
            // puis on ramène ses coins dans le repère de l'image. Un rectangle aligné
            // sur les axes perdrait l'inclinaison, et le texte français serait réécrit
            // droit dans une bulle penchée.
            double cosInverse = Math.Cos(-radians);
            double sinInverse = Math.Sin(-radians);

            double minU = double.MaxValue;
            double maxU = double.MinValue;
            double minV = double.MaxValue;
            double maxV = double.MinValue;

            foreach (Coordonnee coin in TousLesCoins(bloc))
            {
                double u = (coin.X * cosInverse) - (coin.Y * sinInverse);
                double v = (coin.X * sinInverse) + (coin.Y * cosInverse);

                minU = Math.Min(minU, u);
                maxU = Math.Max(maxU, u);
                minV = Math.Min(minV, v);
                maxV = Math.Max(maxV, v);
            }

            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);

            return new Quadrilatere(
                RamenerDansLImage(minU, minV, cos, sin),
                RamenerDansLImage(maxU, minV, cos, sin),
                RamenerDansLImage(maxU, maxV, cos, sin),
                RamenerDansLImage(minU, maxV, cos, sin));
        }

        private static Coordonnee RamenerDansLImage(double u, double v, double cos, double sin)
        {
            return new Coordonnee((u * cos) - (v * sin), (u * sin) + (v * cos));
        }

        private static double AngleMoyen(List<ZoneDeTexte> bloc)
        {
            // On additionne les directions plutôt que les angles : la moyenne
            // arithmétique de 179° et -179° donnerait 0°, alors que la bonne réponse
            // est 180°.
            double sommeX = 0;
            double sommeY = 0;

            foreach (ZoneDeTexte zone in bloc)
            {
                double radians = zone.Quadrilatere.Angle * Math.PI / 180;

                sommeX += Math.Cos(radians);
                sommeY += Math.Sin(radians);
            }

            return Math.Atan2(sommeY, sommeX) * 180 / Math.PI;
        }

        private static IEnumerable<Coordonnee> TousLesCoins(List<ZoneDeTexte> bloc)
        {
            foreach (ZoneDeTexte zone in bloc)
            {
                yield return zone.Quadrilatere.HautGauche;
                yield return zone.Quadrilatere.HautDroit;
                yield return zone.Quadrilatere.BasDroit;
                yield return zone.Quadrilatere.BasGauche;
            }
        }

        #endregion
    }
}
