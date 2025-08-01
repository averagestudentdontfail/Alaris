//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class ArithmeticOISRateHelper : RateHelper {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal ArithmeticOISRateHelper(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.ArithmeticOISRateHelper_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(ArithmeticOISRateHelper obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_ArithmeticOISRateHelper(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public ArithmeticOISRateHelper(uint settlementDays, Period tenor, Frequency fixedLegPaymentFrequency, QuoteHandle fixedRate, OvernightIndex overnightIndex, Frequency overnightLegPaymentFrequency, QuoteHandle spread, double meanReversionSpeed, double volatility, bool byApprox, YieldTermStructureHandle discountingCurve) : this(NQuantLibcPINVOKE.new_ArithmeticOISRateHelper__SWIG_0(settlementDays, Period.getCPtr(tenor), (int)fixedLegPaymentFrequency, QuoteHandle.getCPtr(fixedRate), OvernightIndex.getCPtr(overnightIndex), (int)overnightLegPaymentFrequency, QuoteHandle.getCPtr(spread), meanReversionSpeed, volatility, byApprox, YieldTermStructureHandle.getCPtr(discountingCurve)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ArithmeticOISRateHelper(uint settlementDays, Period tenor, Frequency fixedLegPaymentFrequency, QuoteHandle fixedRate, OvernightIndex overnightIndex, Frequency overnightLegPaymentFrequency, QuoteHandle spread, double meanReversionSpeed, double volatility, bool byApprox) : this(NQuantLibcPINVOKE.new_ArithmeticOISRateHelper__SWIG_1(settlementDays, Period.getCPtr(tenor), (int)fixedLegPaymentFrequency, QuoteHandle.getCPtr(fixedRate), OvernightIndex.getCPtr(overnightIndex), (int)overnightLegPaymentFrequency, QuoteHandle.getCPtr(spread), meanReversionSpeed, volatility, byApprox), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ArithmeticOISRateHelper(uint settlementDays, Period tenor, Frequency fixedLegPaymentFrequency, QuoteHandle fixedRate, OvernightIndex overnightIndex, Frequency overnightLegPaymentFrequency, QuoteHandle spread, double meanReversionSpeed, double volatility) : this(NQuantLibcPINVOKE.new_ArithmeticOISRateHelper__SWIG_2(settlementDays, Period.getCPtr(tenor), (int)fixedLegPaymentFrequency, QuoteHandle.getCPtr(fixedRate), OvernightIndex.getCPtr(overnightIndex), (int)overnightLegPaymentFrequency, QuoteHandle.getCPtr(spread), meanReversionSpeed, volatility), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ArithmeticOISRateHelper(uint settlementDays, Period tenor, Frequency fixedLegPaymentFrequency, QuoteHandle fixedRate, OvernightIndex overnightIndex, Frequency overnightLegPaymentFrequency, QuoteHandle spread, double meanReversionSpeed) : this(NQuantLibcPINVOKE.new_ArithmeticOISRateHelper__SWIG_3(settlementDays, Period.getCPtr(tenor), (int)fixedLegPaymentFrequency, QuoteHandle.getCPtr(fixedRate), OvernightIndex.getCPtr(overnightIndex), (int)overnightLegPaymentFrequency, QuoteHandle.getCPtr(spread), meanReversionSpeed), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ArithmeticOISRateHelper(uint settlementDays, Period tenor, Frequency fixedLegPaymentFrequency, QuoteHandle fixedRate, OvernightIndex overnightIndex, Frequency overnightLegPaymentFrequency, QuoteHandle spread) : this(NQuantLibcPINVOKE.new_ArithmeticOISRateHelper__SWIG_4(settlementDays, Period.getCPtr(tenor), (int)fixedLegPaymentFrequency, QuoteHandle.getCPtr(fixedRate), OvernightIndex.getCPtr(overnightIndex), (int)overnightLegPaymentFrequency, QuoteHandle.getCPtr(spread)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public ArithmeticAverageOIS swap() {
    global::System.IntPtr cPtr = NQuantLibcPINVOKE.ArithmeticOISRateHelper_swap(swigCPtr);
    ArithmeticAverageOIS ret = (cPtr == global::System.IntPtr.Zero) ? null : new ArithmeticAverageOIS(cPtr, true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
