using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using MFiles.VAF;
using MFiles.VAF.Common;
using MFiles.VAF.Configuration;
using MFiles.VAF.Core;
using MFilesAPI;

namespace DIPartners.AccountsPayable.SagePayments
{
    public class InvoiceValue
    {
        public MFIdentifier PropertyID { get; set; }
        public MFConditionType ConditionType { get; set; }
        public String TypedValue { get; set; }

        public InvoiceValue()
        {
            ConditionType = MFConditionType.MFConditionTypeEqual;
        }
    }

    /// <summary>
    /// The entry point for this Vault Application Framework application.
    /// </summary>
    /// <remarks>Examples and further information available on the developer portal: http://developer.m-files.com/. </remarks>
    public class VaultApplication
        : ConfigurableVaultApplicationBase<Configuration>
    {
        #region MFIdentifier
        [MFClass]
        public MFIdentifier Invoice_CD = "vClass.Invoice";
        [MFClass]
        public MFIdentifier Cheque_CD = "vClass.Cheque";
        [MFPropertyDef]
        public MFIdentifier Vendor_PD = "vProperty.Vendor";
        [MFPropertyDef]
        public MFIdentifier InvoiceDate_PD = "vProperty.InvoiceDate";
        [MFPropertyDef]
        public MFIdentifier InvoiceNumber_PD = "vProperty.InvoiceNumber";
        [MFPropertyDef]
        public MFIdentifier Date_PD = "vProperty.Date";
        [MFPropertyDef]
        public MFIdentifier ChequeNumber_PD = "vProperty.ChequeNumber";
        [MFPropertyDef]
        public MFIdentifier ChequeDate_PD = "vProperty.ChequeDate";
        [MFPropertyDef]
        public MFIdentifier ChequeAmount_PD = "vProperty.ChequeAmount";
        [MFPropertyDef]
        public MFIdentifier Amount_PD = "vProperty.Amount";
        [MFPropertyDef]
        public MFIdentifier PaidInvoices_PD = "vProperty.PaidInvoices";
        [MFObjType]
        public MFIdentifier Cheque_OT = "vObject.Cheque";
        #endregion

        [StateAction("vWorkFlowState.SagePayments.NewPayment")]
        public void CreateCheque(StateEnvironment env)
        {
            var Vault = env.ObjVerEx.Vault;
            var oCurrObjVals = Vault.ObjectPropertyOperations.GetProperties(env.ObjVerEx.ObjVer);
            var VendorID = SearchPropertyValue(oCurrObjVals, Vendor_PD);

            List<InvoiceValue> InvoiceValues = new List<InvoiceValue>();            
            InvoiceValues.Add(new InvoiceValue() { PropertyID = Date_PD, TypedValue = SearchPropertyValue(oCurrObjVals, InvoiceDate_PD)  });
            InvoiceValues.Add(new InvoiceValue() { PropertyID = InvoiceNumber_PD, TypedValue = SearchPropertyValue(oCurrObjVals, InvoiceNumber_PD) });
            InvoiceValues.Add(new InvoiceValue() { PropertyID = Vendor_PD, TypedValue = VendorID });
            List<ObjVerEx> InvoiceObjVers = SearchForObjects(env, Invoice_CD, InvoiceValues);

            if (InvoiceObjVers != null)
            {
                InvoiceValues = new List<InvoiceValue>();
                InvoiceValues.Add(new InvoiceValue() { PropertyID = Vendor_PD, TypedValue = VendorID });
                InvoiceValues.Add(new InvoiceValue() { PropertyID = Date_PD, TypedValue = SearchPropertyValue(oCurrObjVals, ChequeDate_PD) });
                InvoiceValues.Add(new InvoiceValue() { PropertyID = ChequeNumber_PD,  TypedValue = SearchPropertyValue(oCurrObjVals, ChequeNumber_PD) });
                InvoiceValues.Add(new InvoiceValue() { PropertyID = Amount_PD, TypedValue = SearchPropertyValue(oCurrObjVals, ChequeAmount_PD) });
                List<ObjVerEx> ChequeObjVers = SearchForObjects(env, Cheque_CD, InvoiceValues);

                ObjVer oCheque;
                var propertyValues = new PropertyValues();

                if (ChequeObjVers != null)
                {
                    oCheque = Vault.ObjectOperations.CheckOut(ChequeObjVers[0].ObjID).ObjVer;
                }
                else
                {
                    var classPropertyValue = new PropertyValue()
                    {
                        PropertyDef = (int)MFBuiltInPropertyDef.MFBuiltInPropertyDefClass
                    };
                    classPropertyValue.Value.SetValue(MFDataType.MFDatatypeLookup, Vault.ClassOperations.GetObjectClass(Cheque_CD).ID);
                    propertyValues.Add(-1, classPropertyValue);

                    propertyValues.Add(-1, GetPropertyValue(oCurrObjVals, ChequeNumber_PD));
                    propertyValues.Add(-1, GetPropertyValue(oCurrObjVals, Date_PD, ChequeDate_PD));
                    propertyValues.Add(-1, GetPropertyValue(oCurrObjVals, Amount_PD, ChequeAmount_PD));
                    propertyValues.Add(-1, GetPropertyValue(oCurrObjVals, Vendor_PD));

                    ObjectVersionAndProperties ppts = Vault.ObjectOperations.CreateNewObject(Cheque_OT, propertyValues);
                    oCheque = ppts.ObjVer;
                }

                var ChequeProps = Vault.ObjectPropertyOperations.GetProperties(oCheque);
                var PaidInvoices = ChequeProps.SearchForProperty(PaidInvoices_PD).TypedValue.GetValueAsLookups();
                bool FoundInvoice = false;

                foreach (var PaidInvoice in PaidInvoices)
                {
                    if(PaidInvoice.ToString() == InvoiceObjVers[0].ObjVer.ID.ToString())
                    {
                        FoundInvoice = true;
                        break;
                    }
                }

                if (!FoundInvoice) {
                    var NewInvoice = new Lookup();

                    NewInvoice.ObjectType = InvoiceObjVers[0].ObjVer.Type;
                    NewInvoice.Item = InvoiceObjVers[0].ObjVer.ID;
                    NewInvoice.DisplayValue = InvoiceObjVers[0].Title;
                    PaidInvoices.Add(-1, NewInvoice);

                    var PaidIvc = new PropertyValue()
                    {
                        PropertyDef = PaidInvoices_PD   //oCurrObjVals.SearchForProperty(PaidInvoices_PD).PropertyDef
                    };
                    PaidIvc.Value.SetValueToMultiSelectLookup(PaidInvoices);
                    Vault.ObjectPropertyOperations.SetProperty(oCheque, PaidIvc);
                }
                env.ObjVerEx.Vault.ObjectOperations.CheckIn(oCheque);
            }
        }

        public List<ObjVerEx> SearchForObjects(StateEnvironment env, MFIdentifier ClassAlias,List <InvoiceValue> Criteria)
        {
            var Vault = env.ObjVerEx.Vault;
            // Create our search builder.
            var searchBuilder = new MFSearchBuilder(Vault);
            // Add an object type filter.
            searchBuilder.Class(ClassAlias);
            // Add a "not deleted" filter.
            searchBuilder.Deleted(false);
            List<ObjVerEx> searchResults;
            foreach (var CriteriaData in Criteria)
            {
                var PropertyType = Vault.PropertyDefOperations.GetPropertyDef(CriteriaData.PropertyID.ID).DataType;
                searchBuilder.Conditions.AddPropertyCondition(
                                                CriteriaData.PropertyID.ID, 
                                                CriteriaData.ConditionType,
                                                PropertyType, 
                                                CriteriaData.TypedValue);
            }

            searchResults = searchBuilder.FindEx();

            return (searchResults.Count != 0) ? searchResults : null;
        }

        public PropertyValue GetPropertyValue(PropertyValues ppvs, MFIdentifier PropertyDef, MFIdentifier SetDef = null)
        {
            var ppValue = new PropertyValue();            
            ppValue.PropertyDef = PropertyDef;

            if(SetDef == null) SetDef = PropertyDef;
            var ppt = ppvs.SearchForProperty(SetDef);
            ppValue.Value.SetValue(ppt.TypedValue.DataType, SearchPropertyValue(ppvs, SetDef, ppt));

            return ppValue;
        }

        public string SearchPropertyValue(PropertyValues ppvs, MFIdentifier def, PropertyValue defaultPpt = null)
        {
            var ppt = defaultPpt;
            if (ppt == null) ppt = ppvs.SearchForProperty(def);
            
            
            return (ppt.TypedValue.DataType == MFDataType.MFDatatypeLookup) ? 
                        ppt.TypedValue.GetLookupID().ToString() : ppt.TypedValue.DisplayValue;
        }

            #region SampleCode
        public ObjectSearchResults SearchObjects_Old(StateEnvironment env, string ClassAlias, List<InvoiceValue> Criteria)
        {
            ObjectSearchResults searchResults;
            var oSearchConditions = new SearchConditions();
            var oSearchCondition = new SearchCondition();
            var Vault = env.ObjVerEx.Vault;
            // Create our search builder.
            var searchBuilder = new MFSearchBuilder(Vault);
            // Add an object type filter.
            searchBuilder.Class(Vault.ClassOperations.GetObjectClassIDByAlias(ClassAlias));
            // Add a "not deleted" filter.
            searchBuilder.Deleted(false);

            int PropertyID;

            foreach (var CriteriaData in Criteria)
            {
                //PropertyID = Vault.PropertyDefOperations.GetPropertyDefIDByAlias(CriteriaData.PropertyName);
                //searchBuilder.Property(PropertyID, CriteriaData.DataType, CriteriaData.TypedValue);

                #region for searchCondition
                //oSearchCondition.ConditionType = MFConditionType.MFConditionTypeEqual;
                //oSearchCondition.Expression.DataPropertyValuePropertyDef = PropertyID;
                //oSearchCondition.TypedValue.SetValue(CriteriaData.DataType, CriteriaData.TypedValue);
                //searchBuilder.Conditions.Add(0, oSearchCondition);  
                //searchResults = searchBuilder.Find();
                #endregion
                //searchBuilder.Conditions.AddPropertyCondition(PropertyID, MFConditionType.MFConditionTypeEqual, CriteriaData.DataType, CriteriaData.TypedValue);
                searchResults = searchBuilder.Find();
            }

            searchResults = searchBuilder.Find();
            return searchResults;
        }

        public bool SearchPrimaryCorporation_Old(StateEnvironment env, string ClassAlias, List<InvoiceValue> Criteria)
        {
            bool bFind = true;
/*            var Vault = env.ObjVerEx.Vault;
            // Create our search builder.
            var searchBuilder = new MFSearchBuilder(Vault);
            // Add an object type filter.
            searchBuilder.Class(Vault.ClassOperations.GetObjectClassIDByAlias(ClassAlias));
            // Add a "not deleted" filter.
            searchBuilder.Deleted(false);

            int PropertyID;

            foreach (var CriteriaData in Criteria)
            {
                PropertyID = Vault.PropertyDefOperations.GetPropertyDefIDByAlias(CriteriaData.PropertyName);
                searchBuilder.Property(PropertyID, CriteriaData.DataType, CriteriaData.TypedValue);
                if (searchBuilder.Find() == null)
                {
                    bFind = false;
                    break;
                }
            }

*/            return bFind;
        }
        public void SearchForObjects_Old(string ClassAlias, List<InvoiceValue> Criteria)
        {
            // Property ID

            // Search Conditions
            var oSearchConditions = new SearchConditions();
            // Search Condition Object used for Searches
            var oSearchCondition = new SearchCondition();


            // Create "Not Deleted" Condition
            {
                // Create the condition.
                oSearchCondition.Expression.SetStatusValueExpression(MFStatusType.MFStatusTypeDeleted);
                oSearchCondition.ConditionType = MFConditionType.MFConditionTypeEqual;
                oSearchCondition.TypedValue.SetValue(MFDataType.MFDatatypeBoolean, false);

                oSearchConditions.Add(-1, oSearchCondition);
            }

            // Create Class Condition
            {
                // Create the search condition.
                oSearchCondition.Expression.DataPropertyValuePropertyDef = (int)MFBuiltInPropertyDef.MFBuiltInPropertyDefClass;
                oSearchCondition.ConditionType = MFConditionType.MFConditionTypeContains;
               // oSearchCondition.TypedValue.SetValue(MFDataType.MFDatatypeLookup, Vault.ClassOperations.GetObjectClassIDByAlias(ClassAlias));
                oSearchConditions.Add(-1, oSearchCondition);
            }

            // Set up Condition for each grouping of Criteria
           /* foreach (var CriteriaData in Criteria)
            {
                //PropertyID = Vault.PropertyDefOperations.GetPropertyDefIDByAlias(CriteriaData.PropertyName);
                // oSearchCondition.Expression.SetStatusValueExpression(MFStatusType.MFStatusTypeObjectTypeID);
                oSearchCondition.ConditionType = MFConditionType.MFConditionTypeEqual;
                oSearchCondition.Expression.DataPropertyValuePropertyDef = PropertyID;
                oSearchCondition.TypedValue.SetValue(CriteriaData.DataType, CriteriaData.TypedValue);
                oSearchConditions.Add(-1, oSearchCondition);
            }*/

            //return gVault.ObjectSearchOperations.SearchForObjectsByConditionsEx(oSearchConditions, MFSearchFlags.MFSearchFlagNone, SortResults: false);
        }

        public static void SampleCode1(StateEnvironment env)
        {
            var Vault = env.ObjVerEx.Vault;

            var ObjTypeContactPerson = Vault.ObjectTypeOperations.GetObjectTypeIDByAlias("vProperty.Vendor");
            var ClassContactPerson = Vault.ClassOperations.GetObjectClassIDByAlias("vProperty.Date");

            // Create our search conditions.
            var searchConditions = new SearchConditions();

            // Add an object type filter.
            {
                // Create the condition.
                var condition = new SearchCondition();
                condition.Expression.SetStatusValueExpression(MFStatusType.MFStatusTypeObjectTypeID);
                condition.ConditionType = MFConditionType.MFConditionTypeEqual;
                condition.TypedValue.SetValue(MFDataType.MFDatatypeLookup, ObjTypeContactPerson);
                searchConditions.Add(-1, condition);
            }

            // Add class filter.
            {
                // Create the search condition.
                var condition = new SearchCondition();
                condition.Expression.DataPropertyValuePropertyDef = (int)MFBuiltInPropertyDef.MFBuiltInPropertyDefClass;
                condition.ConditionType = MFConditionType.MFConditionTypeContains;
                condition.TypedValue.SetValue(MFDataType.MFDatatypeLookup, ClassContactPerson);
                searchConditions.Add(-1, condition);
            }

            // Add a "not deleted" filter.
            {
                // Create the condition.
                var condition = new SearchCondition();
                condition.Expression.SetStatusValueExpression(MFStatusType.MFStatusTypeDeleted);
                condition.ConditionType = MFConditionType.MFConditionTypeEqual;
                condition.TypedValue.SetValue(MFilesAPI.MFDataType.MFDatatypeBoolean, false);

                searchConditions.Add(-1, condition);
            }

            // Execute the search.
            var searchResults = Vault.ObjectSearchOperations.SearchForObjectsByConditionsEx(searchConditions, MFSearchFlags.MFSearchFlagNone, SortResults: false);

            /*List<ClientsInMFiles> ContactPersonList = new List<ClientsInMFiles>();

            foreach (ObjectVersion item in searchResults)
            {
                var LatestProperty = vault.ObjectOperations.GetLatestObjectVersionAndProperties(item.ObjVer.ObjID, true);
                var CompanyName = LatestProperty.Properties.SearchForProperty(875); // Company

                var ContactPerson = item.Title;
                var Company = CompanyName.TypedValue.DisplayValue;

                ContactPersonList.Add(new ClientsInMFiles { ContactPerson = item.Title, Company = Company }); //  + "," + item.ID
            }

            return ContactPersonList;*/

        }
        #endregion

    }
}