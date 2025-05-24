/* -*- mode: c++; tab-width: 4; indent-tabs-mode: nil; c-basic-offset: 4 -*- */

/*
 Copyright (C) 2004 Ferdinando Ametrano
 Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl

 This file is part of QuantLib, a free-software/open-source library
 for financial quantitative analysts and developers - http://quantlib.org/

 QuantLib is free software: you can redistribute it and/or modify it
 under the terms of the QuantLib license.  You should have received a
 copy of the license along with this program; if not, please email
 <quantlib-dev@lists.sf.net>. The license is also available online at
 <http://quantlib.org/license.shtml>.

 This program is distributed in the hope that it will be useful, but WITHOUT
 ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS
 FOR A PARTICULAR PURPOSE.  See the license for more details.
*/

#ifndef quantlib_config_h
#define quantlib_config_h

#define PACKAGE_NAME "QuantLib"
#define PACKAGE_STRING "QuantLib 1.39.0-dev"
#define PACKAGE_TARNAME "QuantLib-1.39.0-dev"
#define PACKAGE_VERSION "1.39.0-dev"
#define PACKAGE_BUGREPORT "https://github.com/lballabio/QuantLib/issues/"

/* #undef QL_ENABLE_PARALLEL_UNIT_TEST_RUNNER */
/* #undef QL_ENABLE_SESSIONS */
/* #undef QL_ENABLE_THREAD_SAFE_OBSERVER_PATTERN */
/* #undef QL_ENABLE_TRACING */
/* #undef QL_ERROR_FUNCTIONS */
/* #undef QL_ERROR_LINES */
/* #undef QL_EXTRA_SAFETY_CHECKS */
/* #undef QL_HIGH_RESOLUTION_DATE */
#define QL_FASTER_LAZY_OBJECTS 1
/* #undef QL_THROW_IN_CYCLES */
/* #undef QL_USE_INDEXED_COUPON */
#define QL_USE_STD_ANY 1
#define QL_USE_STD_OPTIONAL 1
/* #undef QL_USE_STD_SHARED_PTR */
/* #undef QL_NULL_AS_FUNCTIONS */

#endif
