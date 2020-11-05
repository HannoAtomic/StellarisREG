﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace Dauros.StellarisREG.DAL
{
    public class SelectState
    {
        public HashSet<String> SelectedDLC { get; set; } = new HashSet<string>();
        public HashSet<String> EthicNames { get; set; } = new HashSet<String>();
        public IReadOnlyCollection<Ethic> Ethics => EthicNames.Select(en => Ethic.Collection[en]).ToHashSet();
        public String? OriginName { get; set; }
        public Origin? Origin => OriginName != null ? Origin.Collection[OriginName] : null;
        public String? AuthorityName { get; set; }
        public Authority? Authority => AuthorityName != null ? Authority.Collection[AuthorityName] : null;
        public HashSet<String> CivicNames { get; set; } = new HashSet<String>();
        public IReadOnlyCollection<Civic> Civics => CivicNames.Select(en => Civic.Collection[en]).ToHashSet();
        public HashSet<String> TraitNames { get; set; } = new HashSet<String>();
        public IReadOnlyCollection<Trait> Traits => TraitNames.Select(tn => Trait.Collection[tn]).ToHashSet();
        /// <summary>
        /// Contains all EmpireProperties that are set on this SelectState
        /// </summary>
        public HashSet<EmpireProperty> EmpireProperties
        {
            get
            {
                var result = new HashSet<EmpireProperty>();
                result.UnionWith(Ethics);
                if (Origin != null) result.Add(Origin);
                if (Authority != null) result.Add(Authority);
                result.UnionWith(Civics);
                return result;
            }
        }

        public HashSet<EmpireProperty> AllowedEmpireProperties
        {
            get
            {
                return AllEmpireProperties
                    .Where(kvp =>
                    !kvp.Value.Prohibits.Any(e => SelectedDLC.Contains(e))
                    && (!kvp.Value.DLC.Any() || kvp.Value.DLC.All(d=>SelectedDLC.Contains(d))) 
                ).Select(kvp => kvp.Value).ToHashSet();
            }
        }

        public static Dictionary<String, EmpireProperty> AllEmpireProperties
        {
            get
            {
                var result = new Dictionary<String, EmpireProperty>();
                result = result.Union(Ethic.Collection.ToDictionary(kvp => kvp.Key, kvp => kvp.Value as EmpireProperty)).ToDictionary(k => k.Key, k => k.Value);
                result = result.Union(Civic.Collection.ToDictionary(kvp => kvp.Key, kvp => kvp.Value as EmpireProperty)).ToDictionary(k => k.Key, k => k.Value);
                result = result.Union(Origin.Collection.ToDictionary(kvp => kvp.Key, kvp => kvp.Value as EmpireProperty)).ToDictionary(k => k.Key, k => k.Value);
                result = result.Union(Authority.Collection.ToDictionary(kvp => kvp.Key, kvp => kvp.Value as EmpireProperty)).ToDictionary(k => k.Key, k => k.Value);
                result = result.Union(Trait.Collection.ToDictionary(kvp=>kvp.Key, kvp=> kvp.Value as EmpireProperty)).ToDictionary(k => k.Key, k => k.Value);
                return result;
            }
        }

        public SelectState() { }

        public SelectState(HashSet<EmpireProperty> eps)
        {

            foreach (var ep in eps)
            {
                switch (ep.Type)
                {
                    case EmpirePropertyType.Origin:
                        this.OriginName = ep.Name;
                        break;
                    case EmpirePropertyType.Ethic:
                        this.EthicNames.Add(ep.Name);
                        break;
                    case EmpirePropertyType.Authority:
                        this.AuthorityName = ep.Name;
                        break;
                    case EmpirePropertyType.Civic:
                        this.CivicNames.Add(ep.Name);
                        break;
                    case EmpirePropertyType.Trait:
                        break;
                    case EmpirePropertyType.Habitat:
                        break;
                    case EmpirePropertyType.SpeciesArchetype:
                        break;
                    default:
                        break;
                }
            }
        }

        public AndSet GetProhibited()
        {
            var prohibited = new AndSet();

            foreach (var ep in AllowedEmpireProperties)
            {
                //Don't check properties that are already selected
                if (EmpireProperties.Contains(ep)) continue;

                SelectState newState = new SelectState(this.EmpireProperties);
                //Create a selectstate with the new addition

                switch (ep.Type)
                {
                    case EmpirePropertyType.Origin:
                        newState.OriginName = ep.Name;
                        break;
                    case EmpirePropertyType.Ethic:
                        newState.EthicNames.Add(ep.Name);
                        break;
                    case EmpirePropertyType.Authority:
                        newState.AuthorityName = ep.Name;
                        break;
                    case EmpirePropertyType.Civic:
                        newState.CivicNames.Add(ep.Name);
                        break;
                    case EmpirePropertyType.Trait:
                        break;
                    case EmpirePropertyType.Habitat:
                        break;
                    case EmpirePropertyType.SpeciesArchetype:
                        break;
                    default:
                        break;
                }
                if (!newState.ValidateState())
                {
                    prohibited.Add(ep.Name);
                }
            }
            return prohibited;
        }


        public static Boolean ValidateSelection(HashSet<String> selectedEmpirePropertyNames)
        {
            var selectedEmpireProperties = selectedEmpirePropertyNames.Select(n => AllEmpireProperties[n]);

            //If a selectionset contains a selection that is prohibited by that same selectionset, it is invalid.
            var valid = !selectedEmpireProperties.Where(e => e.Prohibits != null).Any(e => e.Prohibits.Any(pe => selectedEmpirePropertyNames.Contains(pe)));
            if (!valid) return valid;

            //two authorities is not allowed
            valid = selectedEmpireProperties.Count(e => e.Type == EmpirePropertyType.Authority) <= 1;
            if (!valid) return valid;

            //two orgins is not allowed
            valid = selectedEmpireProperties.Count(e => e.Type == EmpirePropertyType.Origin) <= 1;
            if (!valid) return valid;

            //more than three civics is not allowed
            valid = selectedEmpireProperties.Count(e => e.Type == EmpirePropertyType.Civic) <= 2;
            if (!valid) return valid;

            //Check if Ethic cost is valid
            var selectedEthics = selectedEmpireProperties.Where(ep => ep.Type == EmpirePropertyType.Ethic).Select(ep => (ep as Ethic)!);
            valid = selectedEthics.Sum(e => e.Cost) <= 3;
            if (!valid) return valid;

            

            return valid;
        }

        public Boolean ValidateState()
        {
            var directValidation = ValidateSelection(EmpireProperties.Select(ep => ep.Name).ToHashSet());
            if (directValidation)
                return GetValidShadowStates().Count() > 0;
            else
                return false;
        }

        public HashSet<AndSet> GetValidShadowStates()
        {
            var allPropertySelectedSets = new HashSet<HashSet<AndSet>>();
            var propertiesToCheckRequirements = new HashSet<EmpireProperty>(EmpireProperties);

            foreach (var ep in propertiesToCheckRequirements)
            {
                if (!ep.Requires.Any()) continue;

                var selSets = RecurseRequirement(ep.Requires);
                var validSets = new HashSet<AndSet>();
                foreach (var selSet in selSets)
                {
                    var isValid = ValidateSelection(selSet);
                    if (isValid) validSets.Add(selSet);
                }
                allPropertySelectedSets.Add(validSets);
            }

            var combinedSelectSets = MergeRequirementSetsWithState(allPropertySelectedSets);
            var combinedValidSets = new HashSet<AndSet>();
            foreach (var combiSet in combinedSelectSets)
            {
                var isValid = ValidateSelection(combiSet);
                if (isValid) combinedValidSets.Add(combiSet);
            }

            return combinedValidSets;
        }

        public HashSet<AndSet> MergeRequirementSetsWithState(HashSet<HashSet<AndSet>> remainingRequirements)
        {
            var result = new HashSet<AndSet>();
            if (remainingRequirements.Any())
            {
                var first = remainingRequirements.First();
                foreach (var ep in first)
                {
                    var newRemaining = remainingRequirements.Where(r => r != first).ToHashSet();
                    if (newRemaining.Count > 0)
                    {
                        var subSets = MergeRequirementSetsWithState(newRemaining);
                        foreach (var sub in subSets)
                        {
                            sub.UnionWith(ep);
                        }
                        result.UnionWith(subSets);
                    }
                    else
                    {
                        var newSet = new AndSet();
                        newSet.UnionWith(EmpireProperties.Select(ep => ep.Name));
                        newSet.UnionWith(ep);
                        result.Add(newSet);
                    }
                }
            }
            else
            {
                var newSet = new AndSet();
                newSet.UnionWith(EmpireProperties.Select(ep => ep.Name));
                result.Add(newSet);
            }
            return result;
        }

        /// <summary>
        /// Returns all possible selection sets for a single EmpireProperty
        /// </summary>
        /// <param name="remaining"></param>
        /// <returns></returns>
        public HashSet<AndSet> RecurseRequirement(HashSet<OrSet> remaining)
        {
            var result = new HashSet<AndSet>();
            var first = remaining.First();
            foreach (var ep in first)
            {
                //Check if this EP has requirements. If it does, add those to the remaining set
                var prop = AllEmpireProperties[ep];
                if (prop.Requires.Any())
                {
                    remaining.UnionWith(prop.Requires);
                }

                var newRemaining = remaining.Where(r => r != first).ToHashSet();
                if (newRemaining.Count > 0)
                {
                    var subSets = RecurseRequirement(newRemaining);
                    foreach (var sub in subSets)
                    {
                        sub.Add(ep);
                    }
                    result.UnionWith(subSets);
                }
                else
                {

                    result.Add(new AndSet() { ep });
                }
            }
            return result;
        }

    }
}