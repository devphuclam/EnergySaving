import { createContext, useContext, type ReactNode } from 'react'
import { webGateways, type WebGateways } from './webGateways'

const GatewayContext = createContext<WebGateways>(webGateways)

export function GatewayProvider({ gateways, children }: { gateways?: WebGateways; children: ReactNode }) {
  return <GatewayContext.Provider value={gateways ?? webGateways}>{children}</GatewayContext.Provider>
}

export function useWebGateways(): WebGateways {
  return useContext(GatewayContext)
}
