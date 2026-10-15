# Nexos VPN — estudo técnico e orçamento

Proposta de 15 de outubro de 2026. Não foram contratados servidores, criada subscrição, instalados drivers ou alterada a rede do utilizador. A VPN não está operacional.

**Decisão do utilizador: sem custos por agora; manter a VPN indisponível.** As fases que dependem de infraestrutura não avançam nesta revisão.

## Tecnologia

Usar WireGuard oficial. Para Windows, o projeto recomenda o serviço embutível tunnel.dll com o binário oficial WireGuardNT wireguard.dll. Evita criar criptografia ou um driver. A integração requer um serviço Windows com as dependências e SID especificados pelo projeto. Não é um botão de DNS. [Integração oficial](https://www.wireguard.com/embedding/), [serviço embutível](https://github.com/WireGuard/wireguard-windows/blob/master/embeddable-dll-service/README.md).

O instalador futuro explicará e pedirá elevação antes de instalar componentes assinados. O otimizador funcionará sem esses componentes. A configuração de arranque só será escolhida após validar criação/remoção do túnel sob pedido, recuperação e compatibilidade com a distribuição NEXUS. Não se adiciona já um serviço permanente.

O código WireGuardNT é GPLv2; os binários oficiais têm uma licença mais permissiva incluída no arquivo. Antes de redistribuir: fixar versão, verificar assinaturas/hashes e rever licenças/avisos de cada componente. Não assumir que tudo é MIT por o código NEXUS ser MIT. Nenhum componente WireGuard foi incorporado nesta revisão. [Licenciamento e distribuição](https://git.zx2c4.com/wireguard-nt/about/).

## Piloto proposto e orçamento

Um VPS Linux europeu com WireGuard oficial, saída IPv4, API HTTPS de registo/revogação de dispositivos e DNS no túnel. API e primeiro nó podem partilhar o VPS apenas no piloto, com permissões e segredos separados; a expansão deve separar controlo e nós de saída. É uma proposta de arquitetura, não uma alteração executada.

Começar com uma única localização real escolhida quando o alojamento for aprovado. Portugal, Espanha, França, Alemanha, Países Baixos, Reino Unido e EUA são objetivos futuros; não aparecem como servidores disponíveis.

| Item | Referência / estimativa |
|---|---|
| Hetzner CX23 na UE | 5,49 €/mês sem IVA e sem IPv4, segundo a tabela de junho de 2026 |
| IPv4 e impostos | Confirmar o endereço na encomenda; reservar aproximadamente 7–10 €/mês no total do nó piloto |
| API no mesmo VPS de testes | Consome recursos do mesmo nó; sem segundo servidor nesta fase |
| Tráfego | A oferta europeia anuncia pelo menos 20 TB incluídos; confirmar excesso, medição e uso permitido |
| DNS/domínio, cópias e monitorização externa | Opcionais ou adicionais; não incluídos automaticamente no intervalo |
| Manutenção e suporte | Trabalho não incluído no preço do alojamento |
| Mais localizações / redundância | Mais nós, IPv4 e manutenção; não incluídos |

Os 7–10 € são uma **estimativa de engenharia para o nó de testes**, não um orçamento fechado de serviço público. Não prometem largura de banda sustentada, concorrência ou disponibilidade. Confirmar uso de VPN/saída permitido, excesso, impostos, faturação e limites antes de contratar.

Fontes: [preços Hetzner](https://docs.hetzner.com/general/infrastructure-and-availability/price-adjustment/), [tráfego na UE](https://www.hetzner.com/cloud/cost-optimized/), [endereços primários](https://docs.hetzner.com/cloud/servers/primary-ips/overview/). [OVHcloud](https://www.ovhcloud.com/fr/vps/) é uma alternativa a avaliar; não há proposta fixa por não existir configuração escolhida.

Não usar um servidor de demonstração de terceiros como infraestrutura para tráfego de utilizadores. Antes de contratar será necessária autorização do orçamento, fornecedor, condições e arquitetura.

## Contas e chaves

No piloto privado, convites e registo de dispositivos podem substituir contas públicas. Serviço público requer contas, limites, sessões, prevenção de abuso, revogação e suporte. Não usar uma chave comum.

Gerar a chave do dispositivo com a implementação oficial, localmente. A chave privada não sai do dispositivo nem entra em SQLite, logs, exportações ou código-fonte. Planeia-se proteção Windows DPAPI e ACLs mínimas compatíveis com o serviço. Não há cofre VPN implementado ou validado nesta revisão.

A API entrega configuração só ao dispositivo autenticado, com TLS validado, expiração e revogação. Os nós conservam apenas peers autorizados. Segredos de servidor/API ficam em armazenamento restrito no servidor. Atualizações do catálogo e dos componentes precisam de verificar integridade, origem e versão.

## Rede, segurança e estados

Começar por túnel integral. Split tunneling por processo fica indisponível até existir método Windows suportado e testes próprios; rotas por aplicação não se resolvem só com AllowedIPs.

Antes de mostrar Ligada/Protegida, verificar:

1. Adaptador/túnel efetivo e handshake recente.
2. Saída real e IP público medido por destino documentado, com consentimento.
3. DNS no túnel e IPv6 completo ou bloqueio limitado à sessão com restauro.
4. Contadores e tempos obtidos da API real do túnel.

Handshake não comprova ausência de fugas. Ligação rápida considera apenas servidores publicados/disponíveis com medições recentes; o código desta revisão não realiza sondas nem cria servidores.

Kill Switch fica indisponível até existir desenho WFP limitado à sessão, regras com proprietário, mecanismo de emergência e recuperação validada. Não criar bloqueios persistentes genéricos. Testar desligar, fechar, suspender, reiniciar e desinstalar.

## Privacidade proposta

Ainda não é uma política de serviço em produção. Minimizar dados a identificador de dispositivo, chave pública, nó atribuído, estado e contadores necessários. Não recolher conteúdos, destinos, navegação ou consultas DNS por defeito.

Retenção proposta a confirmar antes do piloto: erros técnicos sem segredos até sete dias; métricas agregadas de disponibilidade/capacidade até 30 dias. A revogação remove o peer ativo e deve definir a retenção dos registos administrativos. O alojador e o operador podem tratar IPs de origem/saída. Responsáveis, localização, dados e retenção reais precisam de constar da política publicada antes de permitir tráfego.

Não há alegações de no logs, anonimato, menor ping ou proteção absoluta.

## Fases e critérios

| Fase | Estado desta revisão | Para avançar |
|---|---|---|
| 1. Estudo | Proposta e orçamento preparados | Aprovar custo, fornecedor e arquitetura |
| 2. Interface | Página real mostra indisponibilidade; Ligar desativado | Validar renderização e mensagens |
| 3. Ligação | Não implementada | Servidor autorizado, componentes assinados, provisionamento e ligação/desligação reais |
| 4. Segurança | Não validada | DNS/IPv6, quedas, revogação, chaves, reconexão e restauro |
| 5. Integração | Não operacional | Consumo/estabilidade no Windows 11 e instalação/desinstalação validados |

Game Boost nunca ativa a VPN. A função fica desligada e o resto da NEXUS funciona sem contas/servidores VPN.
