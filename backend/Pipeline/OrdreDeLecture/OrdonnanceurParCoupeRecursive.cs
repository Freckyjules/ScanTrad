using ScanTrad.Pipeline.Abstractions;
using ScanTrad.Pipeline.Models;

namespace ScanTrad.Pipeline.OrdreDeLecture
{
    /// <summary>
    /// Retrouve l'ordre de lecture d'une planche en la découpant récursivement le
    /// long de ses gouttières.
    /// </summary>
    /// <remarks>
    /// Le principe, sur une région de la planche :
    /// <list type="number">
    /// <item>chercher une bande horizontale vide qui la traverse de part en part ;</item>
    /// <item>s'il y en a une, traiter la partie du haut en entier, puis celle du bas ;</item>
    /// <item>sinon chercher une bande verticale vide, et traiter d'abord le côté par
    /// lequel on commence à lire ;</item>
    /// <item>si aucune coupe n'est possible, on est dans une case : ordonner ce qui
    /// reste par position.</item>
    /// </list>
    /// <para>
    /// L'horizontale est essayée en premier, et c'est ce qui fait tout fonctionner :
    /// une bande de cases se lit en entier avant qu'on descende à la suivante.
    /// </para>
    /// <para>
    /// Les cases ne sont pas détectées, et il n'y en a pas besoin : les gouttières se
    /// cherchent directement entre les boîtes des bulles. Le découpage se retrouve
    /// tout seul, sans reconnaître le moindre trait. Une case sans texte n'a pas de
    /// rang, ce qui ne gêne personne.
    /// </para>
    /// <para>
    /// Deux mises en page mettent la méthode en échec : une bulle à cheval sur deux
    /// cases, qui bouche la gouttière, et les planches éclatées des scènes d'action,
    /// où il n'y a plus de gouttière du tout. C'est à ces cas-là que sert la
    /// correction manuelle.
    /// </para>
    /// <para>
    /// Il en existe un troisième, plus courant, et il est <em>irréductible</em> sans
    /// détecter les cases : une case haute qui contient deux bulles éloignées. Le
    /// vide qui les sépare est indiscernable d'une vraie séparation entre deux bandes
    /// de cases, et la coupe passe donc au milieu de la case. Sur la planche d'essai,
    /// « AND WHY EXACTLY IS IT THAT I OF ALL PEOPLE… » et « …HAVE TO WORK AS AN
    /// INSTRUCTOR FOR A MAGICAL ACADEMY?! » sont dans la même case mais se retrouvent
    /// séparés par la réplique d'une autre case.
    /// </para>
    /// <para>
    /// Inutile de chercher un seuil : c'est mesuré, l'écart interne à la case valait
    /// 201 pixels et la gouttière verticale entre les deux colonnes 170. Préférer la
    /// gouttière la plus large choisirait encore la mauvaise, et l'écart entre les
    /// deux est bien trop faible pour servir de signal. L'information manque, elle
    /// n'est pas dans la position des bulles. Seule la détection des cases y répond.
    /// </para>
    /// </remarks>
    public class OrdonnanceurParCoupeRecursive : IOrdonnanceurDeZones
    {
        #region Constantes

        /// <summary>
        /// Taille minimale d'une gouttière pour qu'elle soit prise pour une coupe,
        /// exprimée en multiple de la hauteur moyenne des zones de la région.
        /// </summary>
        /// <remarks>
        /// Sans ce plancher, le moindre interligne entre deux bulles d'une même case
        /// serait pris pour une séparation de cases.
        /// </remarks>
        private const double GouttiereMinimale = 0.6;

        #endregion

        #region Méthodes

        /// <inheritdoc />
        public Planche Ordonner(Planche planche)
        {
            if (planche == null)
            {
                throw new ArgumentNullException(nameof(planche));
            }

            List<ZoneDeTexte> ordonnees = new List<ZoneDeTexte>();

            Parcourir(new List<ZoneDeTexte>(planche.Zones), ordonnees, planche.Sens);

            for (int rang = 0; rang < ordonnees.Count; rang++)
            {
                ordonnees[rang].OrdreDeLecture = rang;
            }

            return planche.AvecZones(ordonnees);
        }

        #endregion

        #region Méthodes privées

        private static void Parcourir(List<ZoneDeTexte> region, List<ZoneDeTexte> sortie, SensDeLecture sens)
        {
            if (region.Count <= 1)
            {
                sortie.AddRange(region);
                return;
            }

            // L'horizontale d'abord : une bande de cases se lit en entier avant de
            // descendre. C'est cette priorité qui encode la lecture d'un manga.
            List<List<ZoneDeTexte>>? bandes = Decouper(region, Verticalement: false);

            if (bandes != null)
            {
                foreach (List<ZoneDeTexte> bande in bandes)
                {
                    Parcourir(bande, sortie, sens);
                }

                return;
            }

            List<List<ZoneDeTexte>>? colonnes = Decouper(region, Verticalement: true);

            if (colonnes != null)
            {
                if (sens == SensDeLecture.DroiteAGauche)
                {
                    colonnes.Reverse();
                }

                foreach (List<ZoneDeTexte> colonne in colonnes)
                {
                    Parcourir(colonne, sortie, sens);
                }

                return;
            }

            sortie.AddRange(OrdonnerDansUneCase(region, sens));
        }

        private static List<List<ZoneDeTexte>>? Decouper(List<ZoneDeTexte> region, bool Verticalement)
        {
            double gouttiere = GouttiereMinimale * HauteurMoyenne(region);

            List<ZoneDeTexte> parDebut = region
                .OrderBy(zone => Debut(zone, Verticalement))
                .ToList();

            List<List<ZoneDeTexte>> morceaux = new List<List<ZoneDeTexte>>();
            List<ZoneDeTexte> courant = new List<ZoneDeTexte> { parDebut[0] };
            double finCourante = Fin(parDebut[0], Verticalement);

            for (int i = 1; i < parDebut.Count; i++)
            {
                ZoneDeTexte zone = parDebut[i];

                if (Debut(zone, Verticalement) - finCourante >= gouttiere)
                {
                    morceaux.Add(courant);
                    courant = new List<ZoneDeTexte>();
                }

                courant.Add(zone);
                finCourante = Math.Max(finCourante, Fin(zone, Verticalement));
            }

            morceaux.Add(courant);

            // Un seul morceau : il n'y avait pas de gouttière sur cet axe.
            return morceaux.Count > 1 ? morceaux : null;
        }

        private static List<ZoneDeTexte> OrdonnerDansUneCase(List<ZoneDeTexte> region, SensDeLecture sens)
        {
            // Aucune gouttière : les bulles se chevauchent sur les deux axes. On
            // retombe sur la règle simple, du haut vers le bas puis dans le sens de
            // lecture. C'est le seul endroit où une comparaison deux à deux a un sens,
            // parce qu'il n'y a plus de structure à respecter.
            IOrderedEnumerable<ZoneDeTexte> parHauteur = region
                .OrderBy(zone => zone.Rectangle.Centre.Y);

            return sens == SensDeLecture.DroiteAGauche
                ? parHauteur.ThenByDescending(zone => zone.Rectangle.Centre.X).ToList()
                : parHauteur.ThenBy(zone => zone.Rectangle.Centre.X).ToList();
        }

        private static double HauteurMoyenne(List<ZoneDeTexte> region)
        {
            double moyenne = region.Average(zone => zone.Rectangle.Hauteur);

            // Une région de zones dégénérées ne doit pas ramener une gouttière nulle,
            // qui ferait couper au moindre pixel d'écart.
            return moyenne > 0 ? moyenne : 1;
        }

        private static double Debut(ZoneDeTexte zone, bool verticalement)
        {
            Quadrilatere quad = zone.Rectangle;

            return verticalement
                ? Math.Min(quad.HautGauche.X, quad.BasGauche.X)
                : Math.Min(quad.HautGauche.Y, quad.HautDroit.Y);
        }

        private static double Fin(ZoneDeTexte zone, bool verticalement)
        {
            Quadrilatere quad = zone.Rectangle;

            return verticalement
                ? Math.Max(quad.HautDroit.X, quad.BasDroit.X)
                : Math.Max(quad.BasGauche.Y, quad.BasDroit.Y);
        }

        #endregion
    }
}
