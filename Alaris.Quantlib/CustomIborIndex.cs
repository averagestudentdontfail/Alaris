//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class CustomIborIndex : IborIndex {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal CustomIborIndex(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.CustomIborIndex_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(CustomIborIndex obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_CustomIborIndex(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public CustomIborIndex(string familyName, Period tenor, uint settlementDays, Currency currency, Calendar fixingCalendar, Calendar valueCalendar, Calendar maturityCalendar, BusinessDayConvention convention, bool endOfMonth, DayCounter dayCounter, YieldTermStructureHandle h) : this(NQuantLibcPINVOKE.new_CustomIborIndex__SWIG_0(familyName, Period.getCPtr(tenor), settlementDays, Currency.getCPtr(currency), Calendar.getCPtr(fixingCalendar), Calendar.getCPtr(valueCalendar), Calendar.getCPtr(maturityCalendar), (int)convention, endOfMonth, DayCounter.getCPtr(dayCounter), YieldTermStructureHandle.getCPtr(h)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public CustomIborIndex(string familyName, Period tenor, uint settlementDays, Currency currency, Calendar fixingCalendar, Calendar valueCalendar, Calendar maturityCalendar, BusinessDayConvention convention, bool endOfMonth, DayCounter dayCounter) : this(NQuantLibcPINVOKE.new_CustomIborIndex__SWIG_1(familyName, Period.getCPtr(tenor), settlementDays, Currency.getCPtr(currency), Calendar.getCPtr(fixingCalendar), Calendar.getCPtr(valueCalendar), Calendar.getCPtr(maturityCalendar), (int)convention, endOfMonth, DayCounter.getCPtr(dayCounter)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public Calendar valueCalendar() {
    Calendar ret = new Calendar(NQuantLibcPINVOKE.CustomIborIndex_valueCalendar(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public Calendar maturityCalendar() {
    Calendar ret = new Calendar(NQuantLibcPINVOKE.CustomIborIndex_maturityCalendar(swigCPtr), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
