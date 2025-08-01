//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class FlatForward : YieldTermStructure {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal FlatForward(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.FlatForward_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(FlatForward obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_FlatForward(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public FlatForward(Date referenceDate, QuoteHandle forward, DayCounter dayCounter, Compounding compounding, Frequency frequency) : this(NQuantLibcPINVOKE.new_FlatForward__SWIG_0(Date.getCPtr(referenceDate), QuoteHandle.getCPtr(forward), DayCounter.getCPtr(dayCounter), (int)compounding, (int)frequency), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FlatForward(Date referenceDate, QuoteHandle forward, DayCounter dayCounter, Compounding compounding) : this(NQuantLibcPINVOKE.new_FlatForward__SWIG_1(Date.getCPtr(referenceDate), QuoteHandle.getCPtr(forward), DayCounter.getCPtr(dayCounter), (int)compounding), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FlatForward(Date referenceDate, QuoteHandle forward, DayCounter dayCounter) : this(NQuantLibcPINVOKE.new_FlatForward__SWIG_2(Date.getCPtr(referenceDate), QuoteHandle.getCPtr(forward), DayCounter.getCPtr(dayCounter)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FlatForward(Date referenceDate, double forward, DayCounter dayCounter, Compounding compounding, Frequency frequency) : this(NQuantLibcPINVOKE.new_FlatForward__SWIG_3(Date.getCPtr(referenceDate), forward, DayCounter.getCPtr(dayCounter), (int)compounding, (int)frequency), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FlatForward(Date referenceDate, double forward, DayCounter dayCounter, Compounding compounding) : this(NQuantLibcPINVOKE.new_FlatForward__SWIG_4(Date.getCPtr(referenceDate), forward, DayCounter.getCPtr(dayCounter), (int)compounding), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FlatForward(Date referenceDate, double forward, DayCounter dayCounter) : this(NQuantLibcPINVOKE.new_FlatForward__SWIG_5(Date.getCPtr(referenceDate), forward, DayCounter.getCPtr(dayCounter)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FlatForward(int settlementDays, Calendar calendar, QuoteHandle forward, DayCounter dayCounter, Compounding compounding, Frequency frequency) : this(NQuantLibcPINVOKE.new_FlatForward__SWIG_6(settlementDays, Calendar.getCPtr(calendar), QuoteHandle.getCPtr(forward), DayCounter.getCPtr(dayCounter), (int)compounding, (int)frequency), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FlatForward(int settlementDays, Calendar calendar, QuoteHandle forward, DayCounter dayCounter, Compounding compounding) : this(NQuantLibcPINVOKE.new_FlatForward__SWIG_7(settlementDays, Calendar.getCPtr(calendar), QuoteHandle.getCPtr(forward), DayCounter.getCPtr(dayCounter), (int)compounding), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FlatForward(int settlementDays, Calendar calendar, QuoteHandle forward, DayCounter dayCounter) : this(NQuantLibcPINVOKE.new_FlatForward__SWIG_8(settlementDays, Calendar.getCPtr(calendar), QuoteHandle.getCPtr(forward), DayCounter.getCPtr(dayCounter)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FlatForward(int settlementDays, Calendar calendar, double forward, DayCounter dayCounter, Compounding compounding, Frequency frequency) : this(NQuantLibcPINVOKE.new_FlatForward__SWIG_9(settlementDays, Calendar.getCPtr(calendar), forward, DayCounter.getCPtr(dayCounter), (int)compounding, (int)frequency), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FlatForward(int settlementDays, Calendar calendar, double forward, DayCounter dayCounter, Compounding compounding) : this(NQuantLibcPINVOKE.new_FlatForward__SWIG_10(settlementDays, Calendar.getCPtr(calendar), forward, DayCounter.getCPtr(dayCounter), (int)compounding), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FlatForward(int settlementDays, Calendar calendar, double forward, DayCounter dayCounter) : this(NQuantLibcPINVOKE.new_FlatForward__SWIG_11(settlementDays, Calendar.getCPtr(calendar), forward, DayCounter.getCPtr(dayCounter)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
