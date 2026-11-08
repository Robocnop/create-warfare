// Remplace AWFluids d'Advanced Weather 0.1.2 (MIT), même contenu (décompilé avec javap) + une ligne.
// Registrate 1.3.0+67 : noBucket()/noBlock() n'effacent pas les fournisseurs « seau » et « bloc » posés par défaut,
// qui cherchent advancedweather:mercury_bucket (inexistant). Le plugin JEI de Create plantait sur ce seau en
// remplissant les seaux de chaque fluide (catégorie Spout) : toutes ses catégories suivantes disparaissaient de JEI/EMI.
package net.antopfr.advancedweather.content.fluid;

import com.tterrag.registrate.util.entry.FluidEntry;
import net.antopfr.advancedweather.util.AWRegistrate;
import net.minecraft.resources.ResourceLocation;
import net.neoforged.neoforge.fluids.BaseFlowingFluid;

public class AWFluids {
    public static final FluidEntry<BaseFlowingFluid.Flowing> MERCURY = AWRegistrate.get()
            .fluid("mercury",
                    ResourceLocation.fromNamespaceAndPath("advancedweather", "block/mercury_still"),
                    ResourceLocation.fromNamespaceAndPath("advancedweather", "block/mercury_flow"))
            .properties(p -> p.density(13500).viscosity(1500).canSwim(false).canDrown(false).canConvertToSource(false))
            .noBlock()
            .noBucket()
            // Patch Create Warfare : pas de seau, pas de bloc (NeoForge renvoie alors l'air au lieu de planter).
            .fluidProperties(p -> p.bucket(null).block(null))
            .register();

    public static void register() {
    }
}
